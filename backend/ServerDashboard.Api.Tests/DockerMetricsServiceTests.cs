using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class DockerMetricsServiceTests
{
    [Fact]
    public async Task GetContainersAsync_NoDockerDaemonRunning_ReturnsEmptyInsteadOfThrowing()
    {
        // This machine has no Docker installed at all, so this exercises the
        // real "daemon unreachable" fallback path, not just a mock.
        using var service = new DockerMetricsService();

        var containers = await service.GetContainersAsync();

        Assert.Empty(containers);
    }
}
