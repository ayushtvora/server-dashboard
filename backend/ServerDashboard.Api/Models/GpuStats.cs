namespace ServerDashboard.Api.Models;

public record GpuStats(
    bool Available,
    double? UtilizationPercent,
    long? MemoryUsedMb,
    long? MemoryTotalMb,
    double? TemperatureCelsius
);
