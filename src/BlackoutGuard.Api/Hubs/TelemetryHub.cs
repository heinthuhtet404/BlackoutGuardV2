using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BlackoutGuard.Api.Hubs;

public static class TelemetryHubMethods
{
    public const string TelemetryUpdated = "TelemetryUpdated";
    public const string AlarmRaised = "AlarmRaised";
    public const string AlarmCleared = "AlarmCleared";
    public const string DecisionExecuted = "DecisionExecuted";
}

[Authorize]
public class TelemetryHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var facilityId = Context.User?.FindFirstValue("facility_id");
        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, facilityId);
        }

        await base.OnConnectedAsync();
    }

    public Task BroadcastTelemetry(Guid facilityId, object data) =>
        Clients.Group(facilityId.ToString()).SendAsync(
            TelemetryHubMethods.TelemetryUpdated, data);

    public Task BroadcastAlarm(Guid facilityId, object alarm, bool cleared = false) =>
        Clients.Group(facilityId.ToString()).SendAsync(
            cleared ? TelemetryHubMethods.AlarmCleared : TelemetryHubMethods.AlarmRaised,
            alarm);

    public Task BroadcastDecision(Guid facilityId, object decision) =>
        Clients.Group(facilityId.ToString()).SendAsync(
            TelemetryHubMethods.DecisionExecuted, decision);
}
