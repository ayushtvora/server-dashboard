using Microsoft.AspNetCore.SignalR;
using ServerDashboard.Api.Services;

namespace ServerDashboard.Api.Hubs;

// Clients (the Angular app) connect here to receive live "snapshot" pushes.
// The server calls out to clients (see MetricsBroadcaster, added later); this
// hub itself doesn't need any client-callable methods for v1, but it sends
// the current snapshot immediately on connect so the client isn't waiting on
// the next broadcast tick.
public class MetricsHub : Hub
{
    private readonly ISnapshotStore _snapshotStore;

    public MetricsHub(ISnapshotStore snapshotStore)
    {
        _snapshotStore = snapshotStore;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("snapshot", _snapshotStore.Current);
        await base.OnConnectedAsync();
    }
}
