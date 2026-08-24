using Microsoft.AspNetCore.Mvc;
using ServerDashboard.Api.Models;
using ServerDashboard.Api.Services;

namespace ServerDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly ISnapshotStore _snapshotStore;

    public StatusController(ISnapshotStore snapshotStore)
    {
        _snapshotStore = snapshotStore;
    }

    [HttpGet]
    public ActionResult<ServerSnapshot> Get()
    {
        return Ok(_snapshotStore.Current);
    }
}
