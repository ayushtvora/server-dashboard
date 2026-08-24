namespace ServerDashboard.Api.Models;

public record ContainerStats(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    double CpuUsagePercent,
    long MemoryUsageMb
);
