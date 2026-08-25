using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace BlackoutGuard.Application.UseCases.Shedding;

public class ExecuteSheddingUseCase
{
    private readonly EvaluateSheddingUseCase _evaluator;
    private readonly ILoadRepository _loadRepo;
    private readonly IDecisionAuditLogRepository _auditRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;

    public ExecuteSheddingUseCase(
        EvaluateSheddingUseCase evaluator,
        ILoadRepository loadRepo,
        IDecisionAuditLogRepository auditRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _evaluator = evaluator;
        _loadRepo = loadRepo;
        _auditRepo = auditRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
    }

    public async Task<Result<SheddingRecommendationDto>> ExecuteAsync(Guid facilityId, double availableCapacityKw, CancellationToken ct = default)
    {
        // 1. Calculate Shedding Plan using Evaluator
        var evalResult = await _evaluator.ExecuteAsync(facilityId, availableCapacityKw, ct);
        if (!evalResult.IsSuccess)
            return evalResult;

        var plan = evalResult.Value!;
        if (plan.LoadsToShed.Count == 0)
            return Result<SheddingRecommendationDto>.Success(plan);

        // 2. Execute Shedding inside Transaction
        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                foreach (var action in plan.LoadsToShed)
                {
                    var load = await _loadRepo.GetByIdAsync(action.LoadId, facilityId, ct);
                    if (load is null) continue;

                    // Deactivate Target Load
                    load.IsActive = false;
                    await _loadRepo.UpdateAsync(load, ct);

                    // Write Decision Audit Entry
                    var auditEntry = new AuditEntryDto
                    {
                        FacilityId = facilityId,
                        EventType = "LOAD_SHED",
                        Rationale = action.Reason,
                        AffectedLoadId = action.LoadId
                    };
                    await _auditRepo.AddAsync(auditEntry, ct);
                }

                await tx.CommitAsync(ct);
                return Result<SheddingRecommendationDto>.Success(plan);
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }, ct);
    }
}