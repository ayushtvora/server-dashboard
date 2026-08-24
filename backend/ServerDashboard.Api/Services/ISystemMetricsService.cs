using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

public interface ISystemMetricsService
{
    Task<SystemStats> GetCurrentAsync(CancellationToken cancellationToken = default);
}
