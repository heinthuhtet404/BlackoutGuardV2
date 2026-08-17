using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.Services;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.BusinessRules;

public class PriorityBasedLoadSheddingStrategy : IDecisionStrategy
{
    public LoadSheddingDecision Evaluate(GridState gridState, IEnumerable<Load> loads)
    {
        var decisions = new List<RelayDecision>();
        var loadsList = loads.ToList();

        // If Generator is OFF, shed all loads
        if (!gridState.GeneratorOn)
        {
            foreach (var load in loadsList.Where(l => l.IsSheddable))
            {
                decisions.Add(RelayDecision.Shed(
                    load.RelayAddress,
                    $"Generator is OFF. Shedding load: {load.Name}."
                ));
            }
        }
        // If frequency is critical, shed non-critical loads (Priority = "P3")
        else if (gridState.Frequency < 48.5)
        {
            var shedLoads = loadsList
                .Where(l => l.IsSheddable && l.Priority == "P3")  // "P3" = Non-Critical
                .ToList();

            foreach (var load in shedLoads)
            {
                decisions.Add(RelayDecision.Shed(
                    load.RelayAddress,
                    $"Frequency critically low ({gridState.Frequency:F2} Hz). Shedding non-critical load: {load.Name}."
                ));
            }
        }
        // If frequency is warning, shed one non-critical load
        else if (gridState.Frequency < 49.0)
        {
            var shedLoads = loadsList
                .Where(l => l.IsSheddable && l.Priority == "P3")
                .Take(1)
                .ToList();

            foreach (var load in shedLoads)
            {
                decisions.Add(RelayDecision.Shed(
                    load.RelayAddress,
                    $"Frequency warning ({gridState.Frequency:F2} Hz). Shedding load: {load.Name}."
                ));
            }
        }

        return LoadSheddingDecision.Create(decisions);
    }
}