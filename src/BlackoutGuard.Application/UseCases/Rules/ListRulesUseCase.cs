using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Rules;

public class ListRulesUseCase
{
    private readonly IRuleRepository _ruleRepo;

    public ListRulesUseCase(IRuleRepository ruleRepo)
    {
        _ruleRepo = ruleRepo;
    }

    public async Task<Result<List<RuleDto>>> ExecuteAsync(Guid facilityId, CancellationToken ct = default)
    {
        var rules = await _ruleRepo.GetAllByFacilityAsync(facilityId, ct);
        return Result<List<RuleDto>>.Success(rules);
    }
}
