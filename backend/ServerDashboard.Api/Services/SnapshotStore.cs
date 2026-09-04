using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Thread-safe holder for the most recent snapshot. Registered as a singleton
// so the same instance is shared between the background broadcaster (which
// writes to it) and the controller/hub (which read from it).
public class SnapshotStore : ISnapshotStore
{
    private readonly object _lock = new();
    private ServerSnapshot _current = StubSnapshot();

    public ServerSnapshot Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public void Update(ServerSnapshot snapshot)
    {
        lock (_lock)
        {
            _current = snapshot;
        }
    }

    // Placeholder data so the API returns something meaningful before the
    // real metrics-collection services (added in later steps) exist.
    private static ServerSnapshot StubSnapshot() => new(
        TimestampUtc: DateTimeOffset.UtcNow,
        ServerUp: true,
        System: new SystemStats(
            CpuUsagePercent: 0,
            MemoryUsagePercent: 0,
            MemoryTotalMb: 0,
            MemoryUsedMb: 0,
            CpuTemperatureCelsius: null,
            UptimeSeconds: 0
        ),
        Gpu: new GpuStats(
            Available: false,
            UtilizationPercent: null,
            MemoryUsedMb: null,
            MemoryTotalMb: null,
            TemperatureCelsius: null
        ),
        Containers: Array.Empty<ContainerStats>()
    );
}
