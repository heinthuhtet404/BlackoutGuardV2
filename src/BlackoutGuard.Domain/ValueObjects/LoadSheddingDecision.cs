namespace BlackoutGuard.Domain.ValueObjects;

public sealed record LoadSheddingDecision
{
    public static LoadSheddingDecision None { get; } = new();

    public IReadOnlyList<RelayDecision> RelayDecisions { get; init; } =
        Array.Empty<RelayDecision>();

    public bool IsNone => RelayDecisions.Count == 0;

    public static LoadSheddingDecision Create(IEnumerable<RelayDecision> decisions) =>
        new() { RelayDecisions = decisions.ToArray() };
}
