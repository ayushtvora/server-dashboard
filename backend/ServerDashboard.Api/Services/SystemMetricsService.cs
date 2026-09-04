using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Reads CPU and RAM usage from /proc. Linux-only (won't run on Windows/dev
// laptop) — the file I/O here is intentionally thin so the actual math lives
// in ProcStatParser/MemInfoParser, which are unit tested separately.
public class SystemMetricsService : ISystemMetricsService
{
    private const string ProcStatPath = "/proc/stat";
    private const string MemInfoPath = "/proc/meminfo";
    private const string UptimePath = "/proc/uptime";
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

        double uptimeSeconds = UptimeParser.Parse(
            await File.ReadAllTextAsync(UptimePath, cancellationToken));

        return new SystemStats(
            cpuUsagePercent, memoryUsagePercent, memoryTotalMb, memoryUsedMb, cpuTemperatureCelsius,
            uptimeSeconds);
    }

    // Shells out to lm-sensors, the same way GpuMetricsService shells out to
    // nvidia-smi. /sys/class/thermal's ACPI thermal zones aren't populated on
    // every board (confirmed absent on the actual home server this targets),
    // whereas `sensors` reads the CPU's hwmon driver (k10temp/coretemp)
    // directly and normalizes the chip/label naming across AMD and Intel.
    // Not every machine has lm-sensors installed (e.g. this doesn't exist on
    // the Windows dev laptop), so this degrades to null rather than throwing,
    // the same way GpuStats does for "no data available".
    private static async Task<double?> ReadCpuTemperatureCelsiusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sensors",
                Arguments = "-j",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            return SensorsJsonParser.SelectCpuTemperatureCelsius(output);
        }
        catch (Win32Exception)
        {
            // lm-sensors ("sensors") isn't installed / not on PATH.
            return null;
        }
        catch (JsonException)
        {
            // sensors ran but produced output we couldn't parse.
            return null;
        }
    }
}
