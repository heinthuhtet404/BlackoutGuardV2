using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BlackoutGuard.Infrastructure.Simulation;
using BlackoutGuard.Api.Hubs; // 👈 သင့် TelemetryHub တည်ရှိရာ Namespace ထည့်ပေးပါ

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/simulator")]
[Authorize(Roles = "Admin")]
public class SimulatorController : ControllerBase
{
    private readonly SimulatorDataSource _simulator;
    private readonly IHubContext<TelemetryHub> _hubContext; // 👈 1. SignalR HubContext ထည့်သွင်းခြင်း

    public SimulatorController(
        SimulatorDataSource simulator,
        IHubContext<TelemetryHub> hubContext) // 👈 2. Dependency Injection ခေါ်ယူခြင်း
    {
        _simulator = simulator;
        _hubContext = hubContext;
    }

    [HttpPost("telemetry")]
    public async Task<IActionResult> UpdateTelemetry([FromBody] SimulatorTelemetryRequest request)
    {
        // 1. Simulator State ကို Update လုပ်ခြင်း
        _simulator.UpdateTelemetry(request.Frequency, request.Voltage, request.TotalLoad, request.GeneratorOn);

        // 2. ⚡ SignalR Hub မှတစ်ဆင့် Frontend ချိတ်ဆက်ထားသူများအားလုံးဆီ Real-time Broadcast လွှင့်ပေးခြင်း
        await _hubContext.Clients.All.SendAsync("TelemetryUpdated", new
        {
            frequency = request.Frequency,
            voltage = request.Voltage,
            totalLoadKw = request.TotalLoad,
            generatorOn = request.GeneratorOn
        });

        return Ok(new { message = "Telemetry updated successfully" });
    }

    [HttpPost("fault")]
    public async Task<IActionResult> InjectFault([FromBody] FaultRequest request)
    {
        if (request.Preset == "frequency_drop")
        {
            double faultFreq = 47.5;
            double faultVolt = 230;
            double faultLoad = 500;
            bool faultGen = true;

            _simulator.UpdateTelemetry(faultFreq, faultVolt, faultLoad, faultGen);

            // ⚡ Fault Injection ဖြစ်သွားကြောင်းလည်း Real-time Broadcast လွှင့်ပေးခြင်း
            await _hubContext.Clients.All.SendAsync("TelemetryUpdated", new
            {
                frequency = faultFreq,
                voltage = faultVolt,
                totalLoadKw = faultLoad,
                generatorOn = faultGen
            });

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