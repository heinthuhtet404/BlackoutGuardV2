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
        var group = _hubContext.Clients.Group(facilityId.ToString());

        await group.SendAsync(
            TelemetryHubMethods.TelemetryUpdated,
            new
            {
                frequency = gridState.Frequency,
                voltage = gridState.Voltage,
                totalLoadKw = gridState.TotalLoad,
                generatorOn = gridState.GeneratorOn
            },
            ct);

        foreach (var alarm in alarms)
        {
            await group.SendAsync(
                TelemetryHubMethods.AlarmRaised,
                new
                {
                    code = alarm.Code,
                    severity = alarm.Severity,
                    message = alarm.Message,
                    timestampUtc = alarm.TimestampUtc
                },
                ct);
        }

        if (!decision.IsNone)
        {
            await group.SendAsync(
                TelemetryHubMethods.DecisionExecuted,
                new
                {
                    relayDecisions = decision.RelayDecisions.Select(d => new
                    {
                        relayAddress = d.RelayAddress,
                        energize = d.Energize,
                        rationale = d.Reason
                    }),
                    rationale = string.Join("; ", decision.RelayDecisions.Select(d => d.Reason))
                },
                ct);
        }
    }
}
