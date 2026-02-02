using Microsoft.AspNetCore.Mvc;
using SimpleBPM.Monitor.Services;

namespace SimpleBPM.Monitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IMonitorService _monitor;

    public DashboardController(IMonitorService monitor) => _monitor = monitor;

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await _monitor.GetDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("audit-log")]
    public IActionResult GetAuditLog([FromQuery] int count = 100)
    {
        return Ok(_monitor.GetAuditLog(count));
    }
}
