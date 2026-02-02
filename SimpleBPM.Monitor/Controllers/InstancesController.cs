using Microsoft.AspNetCore.Mvc;
using SimpleBPM.Monitor.Models;
using SimpleBPM.Monitor.Services;

namespace SimpleBPM.Monitor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstancesController : ControllerBase
{
    private readonly IMonitorService _monitor;

    public InstancesController(IMonitorService monitor) => _monitor = monitor;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? definitionName = null,
        [FromQuery] string? search = null)
    {
        var result = await _monitor.GetInstancesAsync(page, pageSize, status, definitionName, search);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var instance = await _monitor.GetInstanceAsync(id);
        if (instance == null)
            return NotFound(new { message = $"Instance '{id}' not found" });
        return Ok(instance);
    }

    [HttpGet("{id}/children")]
    public async Task<IActionResult> GetChildren(string id)
    {
        var children = await _monitor.GetChildInstancesAsync(id);
        return Ok(children);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstanceRequest request)
    {
        var result = await _monitor.CreateInstanceAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/terminate")]
    public async Task<IActionResult> Terminate(string id, [FromBody] TerminateRequest request)
    {
        var result = await _monitor.TerminateInstanceAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/force-complete")]
    public async Task<IActionResult> ForceComplete(string id, [FromBody] ForceCompleteNodeRequest request)
    {
        var result = await _monitor.ForceCompleteNodeAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/signal")]
    public async Task<IActionResult> SendSignal(string id, [FromBody] SendSignalRequest request)
    {
        var result = await _monitor.SendSignalAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}/variables")]
    public async Task<IActionResult> SetVariables(string id, [FromBody] SetVariablesRequest request)
    {
        var result = await _monitor.SetVariablesAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> Retry(string id)
    {
        var result = await _monitor.RetryInstanceAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _monitor.DeleteInstanceAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
