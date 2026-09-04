# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A self-hosted dashboard for a home Ubuntu server: shows whether the server is up, live
CPU/RAM/GPU (NVIDIA) usage, and Docker container health. Backend is ASP.NET Core (.NET 8),
frontend is Angular. Live-only for v1 (no historical charting), no auth (LAN-only), pushed
to the browser over SignalR.

The backend runs directly on the Ubuntu server it monitors (reads `/proc`, shells out to
`nvidia-smi`, talks to the local Docker socket) and is deployed via Docker Compose alongside
the user's existing containers. Development happens on a separate Windows laptop that has
no NVIDIA GPU — GPU code must degrade gracefully (`GpuStats.Available = false`) when
`nvidia-smi` isn't present, and real end-to-end GPU/Docker verification only happens once
deployed to the actual server.

## Commands

The .NET SDK is not on PATH in the dev environment — invoke it via full path, or add
`C:\Program Files\dotnet` to PATH first.

```
# from backend/ServerDashboard.Api/
"C:\Program Files\dotnet\dotnet.exe" build
"C:\Program Files\dotnet\dotnet.exe" run

# from backend/ServerDashboard.Api.Tests/
"C:\Program Files\dotnet\dotnet.exe" test
```

`dotnet run` listens on `http://localhost:5035` (from `Properties/launchSettings.json`'s
`http` profile — no `--urls` override needed). `GET http://localhost:5035/api/status`
returns the current `ServerSnapshot` as JSON, and Swagger UI is available at `/swagger` in
Development. 5035 is also what the frontend's dev environment
(`src/environments/environment.development.ts`) and the production nginx config point at,
so keep them in sync if this ever changes.

Stop a leftover `dotnet run` process before rebuilding if you see `MSB3027`/file-locked
build errors (`Get-Process -Id <pid>` / `Stop-Process -Id <pid> -Force`, pid from the
error's "locked by" line) — `dotnet run` doesn't exit when the tool call that started it
ends.

```
# from frontend/server-dashboard-ui/
npm install
npm start            # dev server at http://localhost:4200, calls the API at localhost:5035
npm test             # vitest, via the @angular/build:unit-test builder
npm run build
```

## Architecture

**Data flow**: metric-collection services gather raw stats -> assembled into one
`ServerSnapshot` -> written to `SnapshotStore` (in-memory, thread-safe singleton, the single
source of truth for "the latest known state") -> pushed to all connected clients over the
`MetricsHub` SignalR hub, and also readable synchronously via `GET /api/status` (used for a
client's first paint, before its SignalR connection finishes negotiating).

- `Models/` — plain immutable `record` types (`SystemStats`, `GpuStats`, `ContainerStats`,
  `ServerSnapshot`) with no behavior; this is the shape shared (conceptually) with the
  Angular frontend's TypeScript interfaces.
- `Services/ISnapshotStore` / `SnapshotStore` — the shared state singleton described above.
  Registered via `builder.Services.AddSingleton<ISnapshotStore, SnapshotStore>()` in
  `Program.cs`, injected into both `StatusController` and `MetricsHub`.
- `Hubs/MetricsHub` — SignalR hub at `/hubs/metrics`. Sends the current snapshot to a client
  immediately on connect; ongoing pushes come from the background broadcaster, not from the
  hub itself (the hub has no client-invokable methods for v1).
- `Controllers/StatusController` — `GET /api/status`, reads `ISnapshotStore.Current`.

- `Services/ISystemMetricsService` / `SystemMetricsService` — reads `/proc/stat` (two
  samples ~500ms apart) and `/proc/meminfo` to produce `SystemStats`. The file I/O is
  intentionally thin and Linux-only (untestable on the dev laptop); the actual math lives
  in `ProcStatParser`/`MemInfoParser`, pure static parsing functions with no I/O, covered by
  unit tests in `ServerDashboard.Api.Tests` using sample `/proc` text. This is the pattern
  to follow for `GpuMetricsService`/`DockerMetricsService` too: keep parsing/math separable
  and testable from the actual OS/process/socket calls.
  - `CpuTemperatureCelsius` (nullable, same "no data" convention as `GpuStats`) is read by
    shelling out to `sensors -j` (lm-sensors), the same pattern `GpuMetricsService` uses for
    `nvidia-smi` — catching `Win32Exception` if `sensors` isn't installed and `JsonException`
    if its output can't be parsed. This was chosen over reading
    `/sys/class/thermal/thermal_zone*` directly because the actual home server's board
    doesn't populate ACPI thermal zones at all (confirmed by inspection — only
    `cooling_device*` entries exist, no `thermal_zone*`); `sensors` reads the CPU's hwmon
    driver (`k10temp` on this AMD box, `coretemp` on Intel) instead, and normalizes the
    chip/sensor naming across vendors. Parsing/selection is pure and unit tested in
    `SensorsJsonParser`, which prefers a known CPU chip name (`k10temp`/`coretemp`) and a
    known primary sensor label (`Tctl`/`Tdie`/`Package id 0`/`Physical id 0`) but falls back
    to the first chip/sensor reported otherwise. Requires the `lm-sensors` package inside the
    `dashboard-api` container (installed via `apt-get` in its `Dockerfile`) — it reads the
    same host-shared `/sys/class/hwmon` the host's own `sensors` command does, so no
    container-side `sensors-detect` is needed as long as the CPU's sensor kernel module is
    already loaded on the host.

- `Services/IGpuMetricsService` / `GpuMetricsService` — shells out to `nvidia-smi
  --query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu
  --format=csv,noheader,nounits` via `System.Diagnostics.Process` and parses the CSV.
  Catches `Win32Exception` (thrown when the executable isn't found — the real case on the
  dev laptop) and parse failures, returning `GpuStats.Available = false` instead of
  throwing. CSV parsing lives in the pure, unit-tested `NvidiaSmiParser`; there's also a
  test that calls the real service directly to confirm the fallback path itself works (not
  just the parser), since the dev laptop genuinely has no `nvidia-smi`.

- `Services/IDockerMetricsService` / `DockerMetricsService` — uses the `Docker.DotNet` NuGet
  package against the local Docker socket (`unix:///var/run/docker.sock` on Linux,
  `npipe://./pipe/docker_engine` on Windows) to list containers and pull one-shot stats per
  container. Broadly catches connection failures (no daemon reachable — true on the dev
  laptop, which has no Docker installed) and returns an empty list rather than throwing,
  since a momentary/dev-time Docker outage shouldn't crash the whole app. CPU%/memory-MB
  math is pure and unit tested in `DockerStatsCalculator` (mirrors `docker stats`'s formula:
  `(cpuDelta/systemDelta) * onlineCpus * 100`, and usage-minus-cache for memory); name/field
  mapping is pure and unit tested in `DockerContainerMapper`. As with GPU, there's also a
  test against the real service confirming the empty-list fallback actually happens here.

- `Services/MetricsBroadcaster` — a `BackgroundService` (registered via
  `AddHostedService<MetricsBroadcaster>()`) that polls all three metric services every 3s,
  assembles a new `ServerSnapshot`, writes it to the `SnapshotStore`, and pushes it via
  `IHubContext<MetricsHub>.Clients.All.SendAsync("snapshot", snapshot)`. The gather-and-
  broadcast logic lives in a public `RunOnceAsync` method (rather than only in the usual
  protected `ExecuteAsync`) specifically so a test can invoke one cycle directly without
  waiting on the real interval loop.
  - **Important failure-handling detail, found by actually running the app**: an unhandled
    exception inside a `BackgroundService`'s `ExecuteAsync` stops the *entire host*
    (`HostOptions.BackgroundServiceExceptionBehavior` defaults to `StopHost`) — not just
    that service. `SystemMetricsService` always throws on the Windows dev laptop (no
    `/proc`), which reproduced this immediately. `RunOnceAsync` therefore catches any
    non-cancellation exception from the gather step, logs it via the injected
    `ILogger<MetricsBroadcaster>`, and returns without updating the snapshot or
    broadcasting — leaving the previous snapshot in place and letting the next interval
    tick retry, rather than taking down the whole app over one bad cycle.

**Frontend** (`frontend/server-dashboard-ui`, Angular standalone components + signals):
`TypeScript` interfaces in `src/app/models/server-snapshot.model.ts` mirror the backend's
`record` types 1:1 (`SystemStats`, `GpuStats`, `ContainerStats`, `ServerSnapshot`).

- `Services/SignalRService` wraps a `HubConnection` to `${environment.apiBaseUrl}/hubs/metrics`,
  exposing incoming `"snapshot"` pushes as an RxJS `snapshots$` observable. The connection
  itself is built by a factory function behind an `HUB_CONNECTION_FACTORY` injection token
  (rather than constructed inline) specifically so tests can substitute a fake connection
  without touching real SignalR/WebSocket machinery.
- `Services/DashboardStateService` is the frontend's equivalent of the backend's
  `SnapshotStore`: `init()` subscribes to `SignalRService.snapshots$` and also fires a
  `GET /api/status` HTTP seed, writing either into one `snapshot` signal (last write wins,
  so a SignalR push that arrives before the HTTP seed resolves is not clobbered by it).
  `serverUp`/`system`/`gpu`/`containers` are `computed()` signals derived from it, with safe
  defaults (`false`/`null`/`[]`) before the first snapshot arrives.
- `Components/` — presentational, `input()`-only components with no injected state:
  `server-status-badge`, `cpu-ram-card`, `gpu-card`, `docker-containers-card`. `App` (in
  `app.ts`/`app.html`) injects `DashboardStateService`, calls `init()` in `ngOnInit`, and
  wires its signals into the components' inputs.
- `environments/environment.ts` (production, `apiBaseUrl: ''` — same-origin, since nginx
  reverse-proxies `/api` and `/hubs`) vs. `environment.development.ts`
  (`apiBaseUrl: 'http://localhost:5035'`, swapped in by the `development` build
  configuration's `fileReplacements`) is how the frontend targets the right backend in each
  environment.
- Tests use Vitest (via the `@angular/build:unit-test` builder), with Angular's
  `HttpTestingController` to assert on the `/api/status` request in
  `dashboard-state.service.spec.ts`, and the `HUB_CONNECTION_FACTORY` token swapped for a
  fake hub connection in `signalr.service.spec.ts`.

**Deployment**: two Docker Compose services — `dashboard-api` (needs access to
`/var/run/docker.sock` and to invoke the host's `nvidia-smi`; `network_mode: host` is the
simplest way to get both without GPU-container-passthrough complexity) and `dashboard-ui`
(Angular build served by nginx, which also reverse-proxies `/api` and `/hubs` to
`dashboard-api` so the browser never needs cross-origin calls in production). In
Development, CORS is opened for `http://localhost:4200` (the Angular dev server origin)
instead.

## Conventions

- Data models are C# `record` types, not classes — they're pure data, no behavior.
- Nullable reference/value types are enabled project-wide (`<Nullable>enable</Nullable>`);
  `GpuStats`'s numeric fields are nullable (`double?`, `long?`) specifically to represent
  "no GPU data available" distinctly from a real zero value.
