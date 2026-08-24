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
```

Once running, `GET http://localhost:5080/api/status` returns the current `ServerSnapshot`
as JSON. Swagger UI is available at `/swagger` in Development.

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

**Not yet implemented**: a `BackgroundService` (`MetricsBroadcaster`) that polls the
metric-collection services on an interval (~2-3s), assembles a new `ServerSnapshot`, writes
it to the `SnapshotStore`, and pushes it to all clients via
`IHubContext<MetricsHub>.Clients.All.SendAsync("snapshot", snapshot)`. The metric-collection
services it will depend on:
- `SystemMetricsService` — CPU % and RAM % from `/proc/stat` (two samples ~1s apart) and
  `/proc/meminfo`. Linux-only; cannot be exercised on the Windows dev laptop.
- `GpuMetricsService` — shells out to
  `nvidia-smi --query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits`
  and parses the CSV. Must return `GpuStats.Available = false` rather than throwing when the
  binary isn't found (true on the dev laptop; the CSV-parsing logic should still be unit
  testable there against sample output).
- `DockerMetricsService` — uses the `Docker.DotNet` NuGet package against
  `unix:///var/run/docker.sock` to list containers with state/health/CPU/mem.

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
