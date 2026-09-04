using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ServerDashboard.Api.Hubs;
using ServerDashboard.Api.Models;
using ServerDashboard.Api.Services;
using Xunit;

namespace ServerDashboard.Api.Tests;

public class MetricsBroadcasterTests
{
    private static readonly SystemStats SampleSystemStats = new(
        CpuUsagePercent: 12.3, MemoryUsagePercent: 45.6, MemoryTotalMb: 16000, MemoryUsedMb: 7296,
        CpuTemperatureCelsius: 55.0);

    private static readonly GpuStats SampleGpuStats = new(
        Available: true, UtilizationPercent: 5, MemoryUsedMb: 100, MemoryTotalMb: 8000, TemperatureCelsius: 40);

    private static readonly IReadOnlyList<ContainerStats> SampleContainers = new[]
    {
        new ContainerStats("id1", "plex", "plexinc/pms-docker", "running", "Up 3 days", 1.5, 512)
    };

    private static (
        MetricsBroadcaster Broadcaster,
        ISnapshotStore SnapshotStore,
        Mock<IClientProxy> ClientProxy
    ) CreateBroadcaster()
    {
        var systemService = new Mock<ISystemMetricsService>();
        systemService.Setup(s => s.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleSystemStats);

        var gpuService = new Mock<IGpuMetricsService>();
        gpuService.Setup(s => s.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleGpuStats);

        var dockerService = new Mock<IDockerMetricsService>();
        dockerService.Setup(s => s.GetContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleContainers);

        var snapshotStore = new SnapshotStore();

        var clientProxy = new Mock<IClientProxy>();
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.All).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<MetricsHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var broadcaster = new MetricsBroadcaster(
            systemService.Object,
            gpuService.Object,
            dockerService.Object,
            snapshotStore,
            hubContext.Object,
            NullLogger<MetricsBroadcaster>.Instance);

        return (broadcaster, snapshotStore, clientProxy);
    }

    [Fact]
    public async Task RunOnceAsync_CombinesAllThreeServicesIntoOneSnapshot_AndStoresIt()
    {
        var (broadcaster, snapshotStore, _) = CreateBroadcaster();

        await broadcaster.RunOnceAsync(CancellationToken.None);

        var current = snapshotStore.Current;
        Assert.True(current.ServerUp);
        Assert.Equal(SampleSystemStats, current.System);
        Assert.Equal(SampleGpuStats, current.Gpu);
        Assert.Equal(SampleContainers, current.Containers);
    }

    [Fact]
    public async Task RunOnceAsync_BroadcastsTheSnapshotToAllConnectedClients()
    {
        var (broadcaster, _, clientProxy) = CreateBroadcaster();

        await broadcaster.RunOnceAsync(CancellationToken.None);

        clientProxy.Verify(
            p => p.SendCoreAsync(
                "snapshot",
                It.Is<object?[]>(args => args.Length == 1 && args[0] is ServerSnapshot),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_OneServiceThrows_DoesNotCrashAndSkipsThisCycle()
    {
        // A BackgroundService that lets an exception escape ExecuteAsync
        // takes the *entire host* down (ASP.NET Core's default
        // BackgroundServiceExceptionBehavior is StopHost) -- confirmed by
        // actually running the app on this Windows laptop, where
        // SystemMetricsService always throws (no /proc). RunOnceAsync must
        // swallow a failed cycle instead of propagating, so the next
        // interval tick gets a chance to succeed.
        var systemService = new Mock<ISystemMetricsService>();
        systemService.Setup(s => s.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("simulated /proc read failure"));

        var gpuService = new Mock<IGpuMetricsService>();
        gpuService.Setup(s => s.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleGpuStats);

        var dockerService = new Mock<IDockerMetricsService>();
        dockerService.Setup(s => s.GetContainersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleContainers);

        var snapshotStore = new SnapshotStore();
        var previousSnapshot = snapshotStore.Current;

        var clientProxy = new Mock<IClientProxy>();
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.All).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<MetricsHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var broadcaster = new MetricsBroadcaster(
            systemService.Object,
            gpuService.Object,
            dockerService.Object,
            snapshotStore,
            hubContext.Object,
            NullLogger<MetricsBroadcaster>.Instance);

        // Should not throw.
        await broadcaster.RunOnceAsync(CancellationToken.None);

        Assert.Same(previousSnapshot, snapshotStore.Current);
        clientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
