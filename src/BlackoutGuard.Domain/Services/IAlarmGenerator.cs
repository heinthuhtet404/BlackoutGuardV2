using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.Services;

public interface IAlarmGenerator
{
    IReadOnlyList<AlarmEvent> Evaluate(EngineState snapshot, GridState telemetry);
}
