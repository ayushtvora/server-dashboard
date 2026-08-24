using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

public interface IGpuMetricsService
{
    Task<GpuStats> GetCurrentAsync(CancellationToken cancellationToken = default);
}
