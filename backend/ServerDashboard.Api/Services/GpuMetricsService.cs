using System.ComponentModel;
using System.Diagnostics;
using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Shells out to nvidia-smi. On machines without an NVIDIA GPU/driver
// (including the Windows dev laptop), Process.Start throws a Win32Exception
// because the executable can't be found — we catch that and report
// "unavailable" rather than crashing, so the rest of the app degrades
// gracefully instead of requiring a GPU to run at all.
public class GpuMetricsService : IGpuMetricsService
{
    private static readonly GpuStats Unavailable = new(
        Available: false,
        UtilizationPercent: null,
        MemoryUsedMb: null,
        MemoryTotalMb: null,
        TemperatureCelsius: null
    );

    public async Task<GpuStats> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Unavailable;
            }

            string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return Unavailable;
            }

            var reading = NvidiaSmiParser.ParseFirstGpuLine(output);
            return new GpuStats(
                Available: true,
                UtilizationPercent: reading.UtilizationPercent,
                MemoryUsedMb: reading.MemoryUsedMb,
                MemoryTotalMb: reading.MemoryTotalMb,
                TemperatureCelsius: reading.TemperatureCelsius
            );
        }
        catch (Win32Exception)
        {
            // nvidia-smi isn't installed / not on PATH.
            return Unavailable;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            // nvidia-smi ran but produced output we couldn't parse.
            return Unavailable;
        }
    }
}
