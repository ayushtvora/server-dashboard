using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class DockerStatsCalculatorTests
{
    [Fact]
    public void CalculateCpuUsagePercent_HalfOfOneCoreBusyOnFourCores_ReturnsExpectedPercent()
    {
        // Container used 50 out of every 100 "system" ns ticks (50% of one
        // core's worth of time) on a 4-CPU system -> 0.5 * 4 * 100 = 200%
        // would be wrong intuition; the formula is (cpuDelta/systemDelta) *
        // onlineCpus * 100, where systemDelta is already summed across all
        // CPUs, so a container fully saturating 1 of 4 cores reports 100/4*...
        // Use round numbers instead of reasoning about real-world meaning:
        double result = DockerStatsCalculator.CalculateCpuUsagePercent(
            cpuTotalUsage: 150,
            previousCpuTotalUsage: 100, // cpuDelta = 50
            systemCpuUsage: 1000,
            previousSystemCpuUsage: 800, // systemDelta = 200
            onlineCpuCount: 4);

        // (50 / 200) * 4 * 100 = 100
        Assert.Equal(100, result, precision: 3);
    }

    [Fact]
    public void CalculateCpuUsagePercent_NoSystemTimeElapsed_ReturnsZeroInsteadOfDividingByZero()
    {
        double result = DockerStatsCalculator.CalculateCpuUsagePercent(
            cpuTotalUsage: 150,
            previousCpuTotalUsage: 100,
            systemCpuUsage: 800,
            previousSystemCpuUsage: 800,
            onlineCpuCount: 4);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateMemoryUsageMb_SubtractsCacheFromUsage()
    {
        long usageBytes = 100L * 1024 * 1024; // 100 MB
        long cacheBytes = 30L * 1024 * 1024;  // 30 MB page cache

        long result = DockerStatsCalculator.CalculateMemoryUsageMb(usageBytes, cacheBytes);

        Assert.Equal(70, result);
    }

    [Fact]
    public void CalculateMemoryUsageMb_CacheLargerThanUsage_ClampsToZero()
    {
        long result = DockerStatsCalculator.CalculateMemoryUsageMb(
            memoryUsageBytes: 10L * 1024 * 1024,
            cacheBytes: 50L * 1024 * 1024);

        Assert.Equal(0, result);
    }
}
