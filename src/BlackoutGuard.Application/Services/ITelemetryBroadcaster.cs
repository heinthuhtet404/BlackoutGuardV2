using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Application.Services;

public interface ITelemetryBroadcaster
{
    Task BroadcastTickAsync(
        Guid facilityId,
        GridState gridState,
        LoadSheddingDecision decision,
        IEnumerable<AlarmEvent> alarms,
        CancellationToken ct = default);
}
