using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class DockerContainerMapperTests
{
    [Fact]
    public void ExtractPrimaryName_StripsLeadingSlash()
    {
        var name = DockerContainerMapper.ExtractPrimaryName(new[] { "/my-container" });

        Assert.Equal("my-container", name);
    }

    [Fact]
    public void ExtractPrimaryName_MultipleNames_UsesFirstOne()
    {
        var name = DockerContainerMapper.ExtractPrimaryName(new[] { "/first-name", "/second-name" });

        Assert.Equal("first-name", name);
    }

    [Fact]
    public void ExtractPrimaryName_NoNames_ReturnsEmptyString()
    {
        var name = DockerContainerMapper.ExtractPrimaryName(Array.Empty<string>());

        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public void ToContainerStats_MapsAllFields()
    {
        var createdAtUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        var stats = DockerContainerMapper.ToContainerStats(
            id: "abc123",
            dockerNames: new[] { "/plex" },
            image: "plexinc/pms-docker:latest",
            state: "running",
            status: "Up 3 days",
            createdAtUtc: createdAtUtc,
            cpuUsagePercent: 12.5,
            memoryUsageMb: 512);

        Assert.Equal("abc123", stats.Id);
        Assert.Equal("plex", stats.Name);
        Assert.Equal("plexinc/pms-docker:latest", stats.Image);
        Assert.Equal("running", stats.State);
        Assert.Equal("Up 3 days", stats.Status);
        Assert.Equal(createdAtUtc, stats.CreatedAtUtc);
        Assert.Equal(12.5, stats.CpuUsagePercent);
        Assert.Equal(512, stats.MemoryUsageMb);
    }
}
