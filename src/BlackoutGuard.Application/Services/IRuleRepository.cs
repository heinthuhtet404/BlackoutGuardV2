using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface IRuleRepository
{
    Task<List<RuleDto>> GetAllByFacilityAsync(Guid facilityId, CancellationToken ct = default);
    Task<RuleDto?> GetByIdAsync(Guid ruleId, Guid facilityId, CancellationToken ct = default);
    Task UpdateAsync(RuleDto rule, CancellationToken ct = default);
}
