using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.Services;

public interface IDecisionStrategy
{
    IReadOnlyList<RelayDecision> Evaluate(EngineState snapshot, GridState telemetry);
}
