using System.Data;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Rules;

public class UpdateRuleUseCase
{
    private const double FreqMinBoundary = 45.0;
    private const double FreqMaxBoundary = 55.0;

    private static readonly string[] ValidParameterKeys =
    {
        "FREQ_LOW", "FREQ_HIGH", "VOLT_LOW", "VOLT_HIGH", "LOAD_SHED_TIMER"
    };

    private readonly IRuleRepository _ruleRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;

    public UpdateRuleUseCase(
        IRuleRepository ruleRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _ruleRepo = ruleRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
    }

    public async Task<Result> ExecuteAsync(UpdateRuleRequest request, CancellationToken ct = default)
    {
        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                var rule = await _ruleRepo.GetByIdAsync(request.RuleId, request.FacilityId, ct);
                if (rule is null)
                    return Result.Failure($"Rule {request.RuleId} not found in facility {request.FacilityId}.");

                var parameterKey = request.ParameterKey ?? rule.ParameterKey;
                if (!ValidParameterKeys.Contains(parameterKey))
                    return Result.Failure(
                        $"Invalid parameter_key '{parameterKey}'. Must be one of: FREQ_LOW, FREQ_HIGH, VOLT_LOW, VOLT_HIGH, LOAD_SHED_TIMER.");

                var minValue = request.MinValue ?? rule.MinValue;
                var maxValue = request.MaxValue ?? rule.MaxValue;

                if (minValue > maxValue)
                    return Result.Failure(
                        $"min_value ({minValue}) cannot be greater than max_value ({maxValue}).");

                if (parameterKey is "FREQ_LOW" or "FREQ_HIGH")
                {
                    if (minValue < FreqMinBoundary)
                        return Result.Failure(
                            $"Frequency rules cannot go below {FreqMinBoundary} Hz. Received min_value {minValue} Hz.");

                    if (maxValue > FreqMaxBoundary)
                        return Result.Failure(
                            $"Frequency rules cannot exceed {FreqMaxBoundary} Hz. Received max_value {maxValue} Hz.");
                }

                rule.Name = request.Name ?? rule.Name;
                rule.ParameterKey = parameterKey;
                rule.MinValue = minValue;
                rule.MaxValue = maxValue;
                rule.CooldownSeconds = request.CooldownSeconds ?? rule.CooldownSeconds;
                rule.Unit = request.Unit ?? rule.Unit;
                rule.IsActive = request.IsActive ?? rule.IsActive;

                await _ruleRepo.UpdateAsync(rule, ct);
                await tx.CommitAsync(ct);

                return Result.Success();
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }, ct);
    }
}
