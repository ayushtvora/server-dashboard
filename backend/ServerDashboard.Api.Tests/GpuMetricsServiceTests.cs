using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class GpuMetricsServiceTests
{
    [Fact]
    public async Task GetCurrentAsync_NvidiaSmiNotInstalled_ReturnsUnavailableInsteadOfThrowing()
    {
        // This machine has no NVIDIA GPU/driver, so this exercises the real
        // Win32Exception fallback path, not just the parser.
        var service = new GpuMetricsService();

        var stats = await service.GetCurrentAsync();

        Assert.False(stats.Available);
        Assert.Null(stats.UtilizationPercent);
    }
}
