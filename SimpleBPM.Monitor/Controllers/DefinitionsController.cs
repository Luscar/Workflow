using Microsoft.AspNetCore.Mvc;
using SimpleBPM.Monitor.Services;

namespace SimpleBPM.Monitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DefinitionsController : ControllerBase
{
    private readonly IMonitorService _monitor;

    public DefinitionsController(IMonitorService monitor) => _monitor = monitor;

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_monitor.GetAllDefinitions());
    }

    [HttpGet("{name}")]
    public IActionResult GetByName(string name, [FromQuery] string? version = null)
    {
        var definition = _monitor.GetDefinition(name, version);
        if (definition == null)
            return NotFound(new { message = $"Definition '{name}' not found" });
        return Ok(definition);
    }
}
