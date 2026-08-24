using Microsoft.AspNetCore.SignalR;
using ServerDashboard.Api.Hubs;
using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Runs for the lifetime of the app (a "hosted service" / BackgroundService),
// periodically gathering fresh metrics and pushing them to SignalR clients.
// The single gather-and-broadcast cycle is pulled out into RunOnceAsync (public,
// rather than the usual protected ExecuteAsync) so it can be called directly
// from a test, instead of waiting on the real interval loop.
public class MetricsBroadcaster : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly ISystemMetricsService _systemMetricsService;
    private readonly IGpuMetricsService _gpuMetricsService;
    private readonly IDockerMetricsService _dockerMetricsService;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IHubContext<MetricsHub> _hubContext;
    private readonly ILogger<MetricsBroadcaster> _logger;

    public MetricsBroadcaster(
        ISystemMetricsService systemMetricsService,
        IGpuMetricsService gpuMetricsService,
        IDockerMetricsService dockerMetricsService,
        ISnapshotStore snapshotStore,
        IHubContext<MetricsHub> hubContext,
        ILogger<MetricsBroadcaster> logger)
    {
        _systemMetricsService = systemMetricsService;
        _gpuMetricsService = gpuMetricsService;
        _dockerMetricsService = dockerMetricsService;
        _snapshotStore = snapshotStore;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        ServerSnapshot snapshot;
        try
        {
            Task<SystemStats> systemStatsTask = _systemMetricsService.GetCurrentAsync(cancellationToken);
            Task<GpuStats> gpuStatsTask = _gpuMetricsService.GetCurrentAsync(cancellationToken);
            Task<IReadOnlyList<ContainerStats>> containersTask = _dockerMetricsService.GetContainersAsync(cancellationToken);

            await Task.WhenAll(systemStatsTask, gpuStatsTask, containersTask);

            snapshot = new ServerSnapshot(
                TimestampUtc: DateTimeOffset.UtcNow,
                ServerUp: true,
                System: systemStatsTask.Result,
                Gpu: gpuStatsTask.Result,
                Containers: containersTask.Result
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Don't let a single bad cycle propagate: an unhandled exception
            // here would stop ExecuteAsync, and ASP.NET Core's default
            // BackgroundServiceExceptionBehavior (StopHost) then shuts down
            // the *entire app*, not just this service. Skipping the cycle
            // lets the next interval tick retry.
            _logger.LogError(ex, "Failed to gather metrics for this broadcast cycle; keeping previous snapshot.");
            return;
        }

        _snapshotStore.Update(snapshot);
        await _hubContext.Clients.All.SendAsync("snapshot", snapshot, cancellationToken);
    }
}
