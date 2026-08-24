using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

// Pure mapping from raw Docker Engine API fields to our ContainerStats model.
// Deliberately takes plain values rather than Docker.DotNet's response types,
// so it's unit testable without constructing those library objects.
public static class DockerContainerMapper
{
    public static ContainerStats ToContainerStats(
        string id,
        IEnumerable<string> dockerNames,
        string image,
        string state,
        string status,
        double cpuUsagePercent,
        long memoryUsageMb)
    {
        return new ContainerStats(
            Id: id,
            Name: ExtractPrimaryName(dockerNames),
            Image: image,
            State: state,
            Status: status,
            CpuUsagePercent: cpuUsagePercent,
            MemoryUsageMb: memoryUsageMb
        );
    }

    // The Docker Engine API prefixes container names with "/", e.g.
    // "/my-container". A container can have multiple names; we show the
    // first one.
    public static string ExtractPrimaryName(IEnumerable<string> dockerNames)
    {
        var raw = dockerNames.FirstOrDefault() ?? string.Empty;
        return raw.TrimStart('/');
    }
}
