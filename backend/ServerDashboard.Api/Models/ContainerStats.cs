namespace ServerDashboard.Api.Models;

public record ContainerStats(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    DateTimeOffset CreatedAtUtc,
    double CpuUsagePercent,
    long MemoryUsageMb
);
