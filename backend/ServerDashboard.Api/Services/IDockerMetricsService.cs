using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

public interface IDockerMetricsService
{
    Task<IReadOnlyList<ContainerStats>> GetContainersAsync(CancellationToken cancellationToken = default);
}
