namespace BlackoutGuard.Domain.Entities;

public sealed record Rule(
    Guid Id,
    Guid FacilityId,
    string ParameterKey,
    double MinValue,
    double MaxValue,
    int CooldownSeconds,
    bool IsActive
);
