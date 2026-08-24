namespace ServerDashboard.Api.Services;

// Pure parsing/math for /proc/stat — no file I/O, so it can be unit tested
// on any OS. CpuUsagePercent needs two samples (see SystemMetricsService),
// because /proc/stat exposes cumulative counters since boot, not a percentage.
public static class ProcStatParser
{
    public readonly record struct CpuTimes(long Idle, long Total);

    public static CpuTimes ParseAggregateCpuLine(string procStatContents)
    {
        // The first line of /proc/stat is the aggregate across all cores:
        // "cpu  user nice system idle iowait irq softirq steal guest guest_nice"
        var line = procStatContents
            .Split('\n')
            .First(l => l.StartsWith("cpu ", StringComparison.Ordinal));

        var values = line
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1) // drop the "cpu" label
            .Select(long.Parse)
            .ToArray();

        // Indexes: 0=user 1=nice 2=system 3=idle 4=iowait 5=irq 6=softirq 7=steal ...
        long idle = values[3] + values[4];
        long total = values.Sum();

        return new CpuTimes(idle, total);
    }

    public static double CalculateUsagePercent(CpuTimes previous, CpuTimes current)
    {
        long totalDelta = current.Total - previous.Total;
        long idleDelta = current.Idle - previous.Idle;

        if (totalDelta <= 0)
        {
            return 0;
        }

        return (1.0 - (double)idleDelta / totalDelta) * 100.0;
    }
}
