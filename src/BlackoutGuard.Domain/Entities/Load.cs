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
    bool IsSheddable,

    // Phase 1 - Criticality Risk Fields (Default Values သတ်မှတ်ထားသဖြင့် Backward Compatible ဖြစ်သည်)
    int SafetyRisk = 1,
    int DataLossRisk = 1,
    int OperationalRisk = 1,
    int ComfortRisk = 1,
    double CriticalityScore = 0
);
