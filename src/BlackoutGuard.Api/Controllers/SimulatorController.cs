using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BlackoutGuard.Infrastructure.Simulation;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/simulator")]
[Authorize(Roles = "Admin")]
public class SimulatorController : ControllerBase
{
    private readonly SimulatorDataSource _simulator;

    public SimulatorController(SimulatorDataSource simulator)
    {
        _simulator = simulator;
    }

    [HttpPost("telemetry")]
    public IActionResult UpdateTelemetry([FromBody] SimulatorTelemetryRequest request)
    {
        _simulator.UpdateTelemetry(request.Frequency, request.Voltage, request.TotalLoad, request.GeneratorOn);
        return Ok(new { message = "Telemetry updated successfully" });
    }

    [HttpPost("fault")]
    public IActionResult InjectFault([FromBody] FaultRequest request)
    {
        if (request.Preset == "frequency_drop")
        {
            _simulator.UpdateTelemetry(47.5, 230, 500, true);
            return Ok(new { message = "Frequency drop fault injected" });
        }
        return BadRequest(new { error = "Unknown fault preset" });
    }
}

public class SimulatorTelemetryRequest
{
    public double Frequency { get; set; }
    public double Voltage { get; set; }
    public double TotalLoad { get; set; }
    public bool GeneratorOn { get; set; }
}

public class FaultRequest
{
    public string Preset { get; set; } = string.Empty;
}