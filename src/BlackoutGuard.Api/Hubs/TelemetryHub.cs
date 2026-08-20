using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var facilityId = Context.User?.FindFirstValue("facility_id");
        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, facilityId);
            _logger.LogInformation("SignalR client {ConnectionId} connected to facility group {FacilityId}", Context.ConnectionId, facilityId);
        }
        else
        {
            _logger.LogWarning("SignalR client {ConnectionId} connected without facility_id claim", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "SignalR client {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("SignalR client {ConnectionId} disconnected cleanly", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
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