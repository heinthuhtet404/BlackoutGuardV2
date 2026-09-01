using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BlackoutGuard.Infrastructure.Simulation;
using BlackoutGuard.Api.Hubs;
using System.Threading.Tasks;

namespace BlackoutGuard.Api.Controllers;

[ApiController]
[Route("api/v1/simulator-data")]
[Authorize(Roles = "Admin")]
public class SimulatorController : ControllerBase
{
    private readonly SimulatorDataSource _simulator;
    private readonly IHubContext<TelemetryHub> _hubContext;
    // 💡 DB အသုံးပြုနေပါက မိမိ၏ DbContext ကို Inject လုပ်နိုင်ပါတယ် (ဥပမာ- AppDbContext)
    // private readonly AppDbContext _context;

    public SimulatorController(
        SimulatorDataSource simulator,
        IHubContext<TelemetryHub> hubContext)
    {
        _simulator = simulator;
        _hubContext = hubContext;
    }

    /// <summary>
    /// 1. DB သို့မဟုတ် Simulator State ထံမှ Config များ ပြန်ထုတ်ပေးသည့် GET Endpoint
    /// </summary>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        // DB သို့မဟုတ် SimulatorDataSource ထဲမှ လက်ရှိ Config ကို ယူယူပါ
        // ဥပမာ - var config = await _context.SimulatorConfigs.FirstOrDefaultAsync();

        var config = _simulator.GetConfig(); // မိမိ၏ Simulator State သို့မဟုတ် DB မှ Data ဆွဲထုတ်ပါ

        if (config == null)
        {
            // DB ထဲမှာ မရှိသေးပါက Default တန်ဖိုးများ ပြန်ပေးမည်
            return Ok(new
            {
                gridOnline = true,
                solarCapacityKw = 50.0,
                generatorCapacityKw = 100.0
            });
        }

        return Ok(new
        {
            gridOnline = config.GridOnline,
            solarCapacityKw = config.SolarCapacityKw,
            generatorCapacityKw = config.GeneratorCapacityKw
        });
    }

    /// <summary>
    /// 2. Frontend မှ ပြောင်းလဲလိုက်သော Config များကို DB သို့ သိမ်းဆည်းသည့် POST Endpoint
    /// </summary>
    [HttpPost("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] SimulatorConfigRequest request)
    {
        // DB သို့မဟုတ် State ထဲသို့ Save လုပ်ပါ
        _simulator.UpdateConfig(request.GridOnline, request.SolarCapacityKw, request.GeneratorCapacityKw);

        // SignalR မှတစ်ဆင့် Online ဖြစ်နေသော Client များအားလုံးဆီ အသိပေးပါ (Option)
        await _hubContext.Clients.All.SendAsync("ConfigUpdated", new
        {
            gridOnline = request.GridOnline,
            solarCapacityKw = request.SolarCapacityKw,
            generatorCapacityKw = request.GeneratorCapacityKw
        });

        return Ok(new { message = "Configuration updated successfully" });
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

// Request Models
public class SimulatorConfigRequest
{
    public bool GridOnline { get; set; }
    public double SolarCapacityKw { get; set; }
    public double GeneratorCapacityKw { get; set; }
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