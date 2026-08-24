using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Reads CPU and RAM usage from /proc. Linux-only (won't run on Windows/dev
// laptop) — the file I/O here is intentionally thin so the actual math lives
// in ProcStatParser/MemInfoParser, which are unit tested separately.
public class SystemMetricsService : ISystemMetricsService
{
    private const string ProcStatPath = "/proc/stat";
    private const string MemInfoPath = "/proc/meminfo";
    private static readonly TimeSpan CpuSampleInterval = TimeSpan.FromMilliseconds(500);

    public async Task<SystemStats> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        // CPU % requires two samples of the cumulative /proc/stat counters,
        // taken a short interval apart.
        var before = ProcStatParser.ParseAggregateCpuLine(
            await File.ReadAllTextAsync(ProcStatPath, cancellationToken));

        await Task.Delay(CpuSampleInterval, cancellationToken);

        var after = ProcStatParser.ParseAggregateCpuLine(
            await File.ReadAllTextAsync(ProcStatPath, cancellationToken));

        double cpuUsagePercent = ProcStatParser.CalculateUsagePercent(before, after);

        var mem = MemInfoParser.Parse(
            await File.ReadAllTextAsync(MemInfoPath, cancellationToken));

        long memoryTotalMb = mem.TotalKb / 1024;
        long memoryUsedMb = (mem.TotalKb - mem.AvailableKb) / 1024;
        double memoryUsagePercent = mem.TotalKb > 0
            ? (double)(mem.TotalKb - mem.AvailableKb) / mem.TotalKb * 100.0
            : 0;

        return new SystemStats(cpuUsagePercent, memoryUsagePercent, memoryTotalMb, memoryUsedMb);
    }
}
