namespace ServerDashboard.Api.Services;

// Pure math for turning Docker's raw counter-based stats into the
// percentages `docker stats` shows. No Docker.DotNet types involved, so this
// is unit testable without a running daemon.
public static class DockerStatsCalculator
{
    // Docker's CPU stats are cumulative nanosecond counters since the
    // container started, both for the container itself and for the whole
    // system. Comparing "current" against "previous" (one API call returns
    // both, since it's this delta or nothing) gives a percentage.
    public static double CalculateCpuUsagePercent(
        long cpuTotalUsage,
        long previousCpuTotalUsage,
        long systemCpuUsage,
        long previousSystemCpuUsage,
        int onlineCpuCount)
    {
        long cpuDelta = cpuTotalUsage - previousCpuTotalUsage;
        long systemDelta = systemCpuUsage - previousSystemCpuUsage;

        if (systemDelta <= 0 || cpuDelta < 0)
        {
            return 0;
        }

        return (double)cpuDelta / systemDelta * onlineCpuCount * 100.0;
    }

    // memoryUsageBytes includes page cache; subtracting it gives the
    // "actual" working-set figure that matches what `docker stats` displays.
    public static long CalculateMemoryUsageMb(long memoryUsageBytes, long cacheBytes)
    {
        long workingSetBytes = Math.Max(memoryUsageBytes - cacheBytes, 0);
        return workingSetBytes / (1024 * 1024);
    }
}
