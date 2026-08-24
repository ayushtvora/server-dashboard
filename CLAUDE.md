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
"C:\Program Files\dotnet\dotnet.exe" run --urls "http://localhost:5080"

# from backend/ServerDashboard.Api.Tests/
"C:\Program Files\dotnet\dotnet.exe" test
```

Once running, `GET http://localhost:5080/api/status` returns the current `ServerSnapshot`
as JSON. Swagger UI is available at `/swagger` in Development.

Stop a leftover `dotnet run` process before rebuilding if you see `MSB3027`/file-locked
build errors (`Get-Process -Id <pid>` / `Stop-Process -Id <pid> -Force`, pid from the
error's "locked by" line) — `dotnet run` doesn't exit when the tool call that started it
ends.

The frontend (`frontend/server-dashboard-ui`, Angular, standalone components + signals) has
not been scaffolded yet — see the build order below.

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

**Not yet implemented**: a `BackgroundService` (`MetricsBroadcaster`) that polls
`SystemMetricsService`/`GpuMetricsService`/`DockerMetricsService` on an interval (~2-3s),
assembles a new `ServerSnapshot`, writes it to the `SnapshotStore`, and pushes it to all
clients via `IHubContext<MetricsHub>.Clients.All.SendAsync("snapshot", snapshot)`. All three
metric services are registered in DI but nothing calls them yet.

**Frontend** (once scaffolded): `SignalRService` wraps a `HubConnection` to `/hubs/metrics`;
`DashboardStateService` seeds from `GET /api/status` then stays updated from the SignalR
stream, exposing Angular signals to presentational components (`server-status-badge`,
`cpu-ram-card`, `gpu-card`, `docker-containers-card`).

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
