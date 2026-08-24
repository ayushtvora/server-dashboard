namespace ServerDashboard.Api.Services;

// Pure parsing for `nvidia-smi --query-gpu=... --format=csv,noheader,nounits`
// output — no process launching, so it's unit testable on any OS.
public static class NvidiaSmiParser
{
    public readonly record struct GpuReading(
        double UtilizationPercent,
        long MemoryUsedMb,
        long MemoryTotalMb,
        double TemperatureCelsius
    );

    // Home servers typically have a single GPU, so we only read the first
    // line. Each line looks like: "45, 2048, 8192, 65"
    // (utilization.gpu, memory.used, memory.total, temperature.gpu)
    public static GpuReading ParseFirstGpuLine(string csvOutput)
    {
        var firstLine = csvOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .First()
            .Trim();

        var fields = firstLine
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(double.Parse)
            .ToArray();

        return new GpuReading(
            UtilizationPercent: fields[0],
            MemoryUsedMb: (long)fields[1],
            MemoryTotalMb: (long)fields[2],
            TemperatureCelsius: fields[3]
        );
    }
}
