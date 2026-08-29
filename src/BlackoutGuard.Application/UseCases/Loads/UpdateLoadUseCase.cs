using System.Data;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Exceptions;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Loads;

public class UpdateLoadUseCase
{
    private readonly ILoadRepository _loadRepo;
    private readonly IDecisionAuditLogRepository _auditRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;
    private readonly LoadSafetyGuard _safetyGuard;

    public UpdateLoadUseCase(
        ILoadRepository loadRepo,
        IFacilityRepository facilityRepo,
        IDecisionAuditLogRepository auditRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _loadRepo = loadRepo;
        _auditRepo = auditRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
        _safetyGuard = new LoadSafetyGuard(loadRepo, facilityRepo);
    }

    public async Task<Result> ExecuteAsync(UpdateLoadRequest request, CancellationToken ct = default)
    {
        if (request.PowerRatingKw is < 0)
            return Result.Failure("Power rating must be >= 0.");

        if (request.RelayAddress is < 0)
            return Result.Failure("Relay address must be >= 0.");

        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                var existing = await _loadRepo.GetByIdAsync(request.LoadId, request.FacilityId, ct);
                if (existing is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result.Failure($"Load {request.LoadId} not found in facility {request.FacilityId}.");
                }

                var relayAddressChanging = request.RelayAddress.HasValue && request.RelayAddress.Value != existing.RelayAddress;
                if (relayAddressChanging)
                {
                    var conflict = await _safetyGuard.FindRelayConflictAsync(
                        request.FacilityId, request.RelayAddress!.Value, request.LoadId, ct);

                    if (conflict is not null)
                    {
                        await tx.RollbackAsync(ct);
                        return Result.Failure(
                            $"Relay address {request.RelayAddress.Value} is already assigned to '{conflict.Name}'");
                    }
                }

                // Capture old state for audit logging
                var oldName = existing.Name;
                var oldPriority = existing.Priority;
                var oldRating = existing.PowerRatingKw;
                var oldRelay = existing.RelayAddress;

                var newPriority = request.Priority ?? existing.Priority;
                var newRating = request.PowerRatingKw ?? existing.PowerRatingKw;
                var priorityUpgradingToP1 = newPriority == "P1" && existing.Priority != "P1";
                var ratingChangingWhileP1 = existing.Priority == "P1" && newPriority == "P1" &&
                                            request.PowerRatingKw.HasValue && request.PowerRatingKw.Value != existing.PowerRatingKw;

                if (priorityUpgradingToP1 || ratingChangingWhileP1)
                {
                    var capacity = await _safetyGuard.EvaluateCapacityAsync(request.FacilityId, newRating, request.LoadId, ct);
                    if (capacity.Facility is null)
                    {
                        await tx.RollbackAsync(ct);
                        return Result.Failure($"Facility {request.FacilityId} not found.");
                    }

                    if (capacity.Deficit > 0)
                    {
                        if (!request.Force)
                        {
                            await tx.RollbackAsync(ct);
                            return Result.Failure(
                                $"P1 capacity exceeded by {capacity.Deficit:F1} kW. " +
                                $"Total P1: {capacity.TotalP1Kw:F1} kW, Capacity: {capacity.Facility.GeneratorCapacityKW:F1} kW. " +
                                $"Use force=true to override.");
                        }

                        var auditOverride = new AuditEntryDto
                        {
                            FacilityId = request.FacilityId,
                            EventType = "CAPACITY_OVERRIDE",
                            Rationale = $"P1 load '{request.Name ?? existing.Name}' updated with force=true. " +
                                       $"Total P1: {capacity.TotalP1Kw:F1} kW exceeds capacity {capacity.Facility.GeneratorCapacityKW:F1} kW by {capacity.Deficit:F1} kW.",
                            AffectedLoadId = request.LoadId
                        };
                        await _auditRepo.AddAsync(auditOverride, ct);
                    }
                }

                existing.Name = request.Name ?? existing.Name;
                existing.RelayAddress = request.RelayAddress ?? existing.RelayAddress;
                existing.PowerRatingKw = newRating;
                existing.Priority = newPriority;
                existing.PriorityMode = request.PriorityMode ?? existing.PriorityMode;
                existing.IsSheddable = request.IsSheddable ?? existing.IsSheddable;

                await _loadRepo.UpdateAsync(existing, ct);

                // Audit Log For Load Update (Matching PascalCase naming like LoadDeleted)
                var updateAudit = new AuditEntryDto
                {
                    FacilityId = request.FacilityId,
                    EventType = "LoadUpdated",
                    Rationale = $"Load '{existing.Name}' updated. Changes -> Name: '{oldName}' -> '{existing.Name}', Priority: '{oldPriority}' -> '{existing.Priority}', Power: {oldRating}kW -> {existing.PowerRatingKw}kW, Relay: {oldRelay} -> {existing.RelayAddress}.",
                    AffectedLoadId = request.LoadId
                };
                await _auditRepo.AddAsync(updateAudit, ct);

                await tx.CommitAsync(ct);
                return Result.Success();
            }
            catch (RelayConflictException ex)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure(ex.Message);
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }, ct);
    }
}