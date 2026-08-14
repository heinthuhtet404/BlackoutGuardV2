namespace BlackoutGuard.Domain.Entities;

public sealed record Load(
    Guid Id,
    Guid FacilityId,
    Guid ZoneId,
    string Name,
    int RelayAddress,
    double PowerRatingKw,
    string Priority,
    string PriorityMode,
    bool IsActive,
    bool IsSheddable
);
