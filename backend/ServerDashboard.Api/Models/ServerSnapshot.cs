namespace ServerDashboard.Api.Models;

public record ServerSnapshot(
    DateTimeOffset TimestampUtc,
    bool ServerUp,
    SystemStats System,
    GpuStats Gpu,
    IReadOnlyList<ContainerStats> Containers
);
