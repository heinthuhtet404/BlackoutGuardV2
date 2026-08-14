using BlackoutGuard.Domain.Entities;

namespace BlackoutGuard.Domain.ValueObjects;

public sealed record EngineState(
    IReadOnlyList<Load> Loads,
    IReadOnlyList<Rule> Rules,
    IReadOnlyDictionary<Guid, LoadCooldownInfo> CooldownStates,
    Guid FacilityId,
    long Version
)
{
    public static EngineState Empty(Guid facilityId) => new(
        Array.Empty<Load>(),
        Array.Empty<Rule>(),
        new Dictionary<Guid, LoadCooldownInfo>(),
        facilityId,
        0);

    internal bool IsValid() =>
        Loads is not null && Rules is not null && CooldownStates is not null;
}

public sealed record LoadCooldownInfo(
    DateTime? LastShedAt,
    DateTime? LastRestoredAt,
    DateTime? CooldownUntil
);
