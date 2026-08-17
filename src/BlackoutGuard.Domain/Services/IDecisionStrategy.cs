using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.Services;

public interface IDecisionStrategy
{
    LoadSheddingDecision Evaluate(GridState gridState, IEnumerable<Load> loads);
}