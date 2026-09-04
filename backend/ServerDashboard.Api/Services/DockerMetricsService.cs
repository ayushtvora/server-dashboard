using Docker.DotNet;
using Docker.DotNet.Models;
using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Talks to the local Docker Engine over its Unix socket (or the Windows
// named pipe, for local dev machines that have Docker Desktop). Connection
// failures — including "no Docker installed at all," true on this dev
// laptop — are treated as "no containers to report" rather than crashing,
// since production (the actual Ubuntu server) always has Docker running.
public class DockerMetricsService : IDockerMetricsService, IDisposable
{
    private readonly DockerClient _client;

    public DockerMetricsService()
    {
        var socketUri = OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");

        _client = new DockerClientConfiguration(socketUri).CreateClient();
    }

    public async Task<IReadOnlyList<ContainerStats>> GetContainersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IList<ContainerListResponse> containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters { All = true },
                cancellationToken);

            var results = new List<ContainerStats>(containers.Count);
            foreach (var container in containers)
            {
                var (cpuPercent, memoryMb) = await GetResourceUsageAsync(container.ID, cancellationToken);

                results.Add(DockerContainerMapper.ToContainerStats(
                    container.ID,
                    container.Names,
                    container.Image,
                    container.State,
                    container.Status,
                    new DateTimeOffset(container.Created.ToUniversalTime()),
                    cpuPercent,
                    memoryMb));
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Daemon unreachable (socket/pipe missing, permission denied,
            // etc.) — degrade to "no container data" rather than crashing.
            return Array.Empty<ContainerStats>();
        }
    }

    private async Task<(double CpuUsagePercent, long MemoryUsageMb)> GetResourceUsageAsync(
        string containerId, CancellationToken cancellationToken)
    {
        ContainerStatsResponse? stats = null;
        var progress = new Progress<ContainerStatsResponse>(response => stats = response);

        await _client.Containers.GetContainerStatsAsync(
            containerId,
            new ContainerStatsParameters { Stream = false },
            progress,
            cancellationToken);

        if (stats is null)
        {
            return (0, 0);
        }

        double cpuUsagePercent = DockerStatsCalculator.CalculateCpuUsagePercent(
            cpuTotalUsage: (long)stats.CPUStats.CPUUsage.TotalUsage,
            previousCpuTotalUsage: (long)stats.PreCPUStats.CPUUsage.TotalUsage,
            systemCpuUsage: (long)stats.CPUStats.SystemUsage,
            previousSystemCpuUsage: (long)stats.PreCPUStats.SystemUsage,
            onlineCpuCount: (int)stats.CPUStats.OnlineCPUs);

        long cacheBytes = stats.MemoryStats.Stats is not null
            && stats.MemoryStats.Stats.TryGetValue("cache", out var cache)
                ? (long)cache
                : 0;

        long memoryUsageMb = DockerStatsCalculator.CalculateMemoryUsageMb(
            (long)stats.MemoryStats.Usage, cacheBytes);

        return (cpuUsagePercent, memoryUsageMb);
    }

    public void Dispose() => _client.Dispose();
}
