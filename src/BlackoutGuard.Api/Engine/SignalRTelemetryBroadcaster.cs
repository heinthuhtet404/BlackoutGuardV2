using BlackoutGuard.Api.Hubs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace BlackoutGuard.Api.Engine;

public class SignalRTelemetryBroadcaster : ITelemetryBroadcaster
{
    private readonly IHubContext<TelemetryHub> _hubContext;

    public SignalRTelemetryBroadcaster(IHubContext<TelemetryHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastTickAsync(
        Guid facilityId,
        GridState gridState,
        LoadSheddingDecision decision,
        IEnumerable<AlarmEvent> alarms,
        CancellationToken ct = default)
    {
        var telemetryPayload = new
        {
            frequency = gridState.Frequency,
            voltage = gridState.Voltage,
            totalLoadKw = gridState.TotalLoad,
            generatorOn = gridState.GeneratorOn
        };

        // 💡 Group အပြင် Connected Client အားလုံးဆီပါ လွှင့်ပေးမည် (Group Filter ကြောင့် Data မပေါ်သည်ကို ဖြေရှင်းရန်)
        await _hubContext.Clients.All.SendAsync(
            TelemetryHubMethods.TelemetryUpdated,
            telemetryPayload,
            ct);

        if (facilityId != Guid.Empty)
        {
            await _hubContext.Clients.Group(facilityId.ToString()).SendAsync(
                TelemetryHubMethods.TelemetryUpdated,
                telemetryPayload,
                ct);
        }

        foreach (var alarm in alarms)
        {
            var alarmData = new
            {
                code = alarm.Code,
                severity = alarm.Severity,
                message = alarm.Message,
                timestampUtc = alarm.TimestampUtc
            };

            await _hubContext.Clients.All.SendAsync(TelemetryHubMethods.AlarmRaised, alarmData, ct);
        }

        if (decision != null && !decision.IsNone)
        {
            var decisionData = new
            {
                relayDecisions = decision.RelayDecisions.Select(d => new
                {
                    relayAddress = d.RelayAddress,
                    energize = d.Energize,
                    rationale = d.Reason
                }),
                rationale = string.Join("; ", decision.RelayDecisions.Select(d => d.Reason))
            };

            await _hubContext.Clients.All.SendAsync(TelemetryHubMethods.DecisionExecuted, decisionData, ct);
        }
    }
}