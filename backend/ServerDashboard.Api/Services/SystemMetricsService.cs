using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Reads CPU and RAM usage from /proc. Linux-only (won't run on Windows/dev
// laptop) — the file I/O here is intentionally thin so the actual math lives
// in ProcStatParser/MemInfoParser, which are unit tested separately.
public class SystemMetricsService : ISystemMetricsService
{
    private const string ProcStatPath = "/proc/stat";
    private const string MemInfoPath = "/proc/meminfo";
    private const string ThermalClassPath = "/sys/class/thermal";
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

        double? cpuTemperatureCelsius = await ReadCpuTemperatureCelsiusAsync(cancellationToken);

        return new SystemStats(
            cpuUsagePercent, memoryUsagePercent, memoryTotalMb, memoryUsedMb, cpuTemperatureCelsius);
    }

    // Not every system exposes thermal zones (e.g. some VMs/containers), so
    // this degrades to null rather than throwing, the same way GpuStats does
    // for "no data available".
    private static async Task<double?> ReadCpuTemperatureCelsiusAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(ThermalClassPath))
        {
            return null;
        }

        var zones = new List<ThermalZoneParser.ThermalZone>();

        foreach (var zoneDir in Directory.EnumerateDirectories(ThermalClassPath, "thermal_zone*"))
        {
            try
            {
                string type = (await File.ReadAllTextAsync(Path.Combine(zoneDir, "type"), cancellationToken)).Trim();
                string temp = await File.ReadAllTextAsync(Path.Combine(zoneDir, "temp"), cancellationToken);
                zones.Add(new ThermalZoneParser.ThermalZone(type, temp));
            }
            catch (IOException)
            {
                // A zone can disappear or be unreadable between the directory
                // listing and the read; just skip it.
            }
        }

        return ThermalZoneParser.SelectCpuTemperatureCelsius(zones);
    }
}
