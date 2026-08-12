using System.Data;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Exceptions;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Loads;

public class CreateLoadUseCase
{
    private readonly ILoadRepository _loadRepo;
    private readonly IFacilityRepository _facilityRepo;
    private readonly IDecisionAuditLogRepository _auditRepo;
    private readonly IDbTransactionFactory _txFactory;
    private readonly IExecutionStrategy _executionStrategy;
    private readonly LoadSafetyGuard _safetyGuard;

    public CreateLoadUseCase(
        ILoadRepository loadRepo,
        IFacilityRepository facilityRepo,
        IDecisionAuditLogRepository auditRepo,
        IDbTransactionFactory txFactory,
        IExecutionStrategy executionStrategy)
    {
        _loadRepo = loadRepo;
        _facilityRepo = facilityRepo;
        _auditRepo = auditRepo;
        _txFactory = txFactory;
        _executionStrategy = executionStrategy;
        _safetyGuard = new LoadSafetyGuard(loadRepo, facilityRepo);
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateLoadRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<Guid>.Failure("Load name is required.");

        if (request.PowerRatingKw < 0)
            return Result<Guid>.Failure("Power rating must be >= 0.");

        if (request.RelayAddress < 0)
            return Result<Guid>.Failure("Relay address must be >= 0.");

        return await _executionStrategy.ExecuteAsync(async () =>
        {
            await using var tx = await _txFactory.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

            try
            {
                var conflict = await _safetyGuard.FindRelayConflictAsync(request.FacilityId, request.RelayAddress, null, ct);
                if (conflict is not null)
                {
                    return Result<Guid>.Failure(
                        $"Relay address {request.RelayAddress} is already assigned to '{conflict.Name}'");
                }

                if (request.Priority == "P1")
                {
                    var capacity = await _safetyGuard.EvaluateCapacityAsync(request.FacilityId, request.PowerRatingKw, null, ct);
                    if (capacity.Facility is null)
                    {
                        return Result<Guid>.Failure($"Facility {request.FacilityId} not found.");
                    }

                    if (capacity.Deficit > 0)
                    {
                        if (!request.Force)
                        {
                            return Result<Guid>.Failure(
                                $"P1 capacity exceeded by {capacity.Deficit:F1} kW. " +
                                $"Total P1: {capacity.TotalP1Kw:F1} kW, Capacity: {capacity.Facility.GeneratorCapacityKW:F1} kW. " +
                                $"Use force=true to override.");
                        }

                        var loadId = Guid.NewGuid();
                        var loadDto = new LoadDto
                        {
                            Id = loadId,
                            FacilityId = request.FacilityId,
                            ZoneId = request.ZoneId,
                            Name = request.Name,
                            RelayAddress = request.RelayAddress,
                            PowerRatingKw = request.PowerRatingKw,
                            Priority = request.Priority,
                            PriorityMode = request.PriorityMode ?? "auto",
                            IsSheddable = request.IsSheddable,
                            IsActive = true
                        };

                        await _loadRepo.AddAsync(loadDto, ct);

                        var auditEntry = new AuditEntryDto
                        {
                            FacilityId = request.FacilityId,
                            EventType = "CAPACITY_OVERRIDE",
                            Rationale = $"P1 load '{request.Name}' created with force=true. " +
                                       $"Total P1: {capacity.TotalP1Kw:F1} kW exceeds capacity {capacity.Facility.GeneratorCapacityKW:F1} kW by {capacity.Deficit:F1} kW.",
                            AffectedLoadId = loadId
                        };

                        await _auditRepo.AddAsync(auditEntry, ct);
                        await tx.CommitAsync(ct);

                        return Result<Guid>.Success(loadId);
                    }
                }

                var newLoadId = Guid.NewGuid();
                var newLoad = new LoadDto
                {
                    Id = newLoadId,
                    FacilityId = request.FacilityId,
                    ZoneId = request.ZoneId,
                    Name = request.Name,
                    RelayAddress = request.RelayAddress,
                    PowerRatingKw = request.PowerRatingKw,
                    Priority = request.Priority,
                    PriorityMode = request.PriorityMode ?? "auto",
                    IsSheddable = request.IsSheddable,
                    IsActive = true
                };

                await _loadRepo.AddAsync(newLoad, ct);
                await tx.CommitAsync(ct);

                return Result<Guid>.Success(newLoadId);
            }
            catch (RelayConflictException ex)
            {
                return Result<Guid>.Failure(ex.Message);
            }
            catch (Exception)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }, ct);
    }
}
