namespace ServerDashboard.Api.Models;

public record SystemStats(
    double CpuUsagePercent,
    double MemoryUsagePercent,
    long MemoryTotalMb,
    long MemoryUsedMb,
    double? CpuTemperatureCelsius
);
