using System.Data;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Loads;

public class DeleteLoadUseCase
{
    private readonly ILoadRepository _loadRepo;
    private readonly IDecisionAuditLogRepository _auditRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;

    public DeleteLoadUseCase(
        ILoadRepository loadRepo,
        IDecisionAuditLogRepository auditRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _loadRepo = loadRepo;
        _auditRepo = auditRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
    }

    public async Task<Result> ExecuteAsync(Guid loadId, Guid facilityId, CancellationToken ct = default)
    {
        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                var load = await _loadRepo.GetByIdAsync(loadId, facilityId, ct);
                if (load is null)
                    return Result.Failure($"Load {loadId} not found in facility {facilityId}.");

                // time_schedules and load_cooldown_state both have ON DELETE CASCADE
                // on load_id per the Task 1.2 schema — rely on the database cascade.
                await _loadRepo.DeleteAsync(loadId, facilityId, ct);

                var auditEntry = new AuditEntryDto
                {
                    FacilityId = facilityId,
                    EventType = "LoadDeleted",
                    Rationale = $"Load '{load.Name}' (priority {load.Priority}, relay address {load.RelayAddress}) deleted from facility {facilityId}.",
                    AffectedLoadId = loadId
                };

                await _auditRepo.AddAsync(auditEntry, ct);
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
