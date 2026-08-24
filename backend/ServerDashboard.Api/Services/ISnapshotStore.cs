using ServerDashboard.Api.Models;

namespace ServerDashboard.Api.Services;

public interface ISnapshotStore
{
    ServerSnapshot Current { get; }
    void Update(ServerSnapshot snapshot);
}
