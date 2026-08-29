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

                // 1. Audit Log ကို အရင်ဆောက်ပါ (Load မပျက်သေးသည့်အတွက် AffectedLoadId ထည့်လို့ရပါသည်)
                var auditEntry = new AuditEntryDto
                {
                    FacilityId = facilityId,
                    EventType = "LoadDeleted",
                    Rationale = $"Load '{load.Name}' (Priority: {load.Priority}, Relay: {load.RelayAddress}) was deleted.",
                    AffectedLoadId = loadId
                };

                await _auditRepo.AddAsync(auditEntry, ct);

                // 2. Audit Log ရေးပြီးမှ Load ကို Delete လုပ်ပါ
                // (Database FK ON DELETE SET NULL ကြောင့် Load ပျက်သွားသော်လည်း Audit Log ကျန်ခဲ့ပြီး AffectedLoadId က NULL ဖြစ်သွားပါမည်)
                await _loadRepo.DeleteAsync(loadId, facilityId, ct);

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
