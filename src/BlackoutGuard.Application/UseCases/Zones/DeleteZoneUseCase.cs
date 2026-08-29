using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class DeleteZoneUseCase
{
    private readonly IZoneRepository _repository;
    private readonly IDecisionAuditLogRepository _auditLogRepository;

    public DeleteZoneUseCase(
        IZoneRepository repository,
        IDecisionAuditLogRepository auditLogRepository)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result> ExecuteAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
    {
        var zone = await _repository.GetByIdAsync(zoneId, facilityId, ct);
        if (zone is null)
            return Result.Failure("Zone not found in this facility.");

        var hasChildren = await _repository.HasChildrenAsync(zoneId, facilityId, ct);
        if (hasChildren)
            return Result.Failure("Cannot delete zone: it has child zones. Remove or reassign child zones first.");

        var hasLoads = await _repository.HasLoadsAsync(zoneId, facilityId, ct);
        if (hasLoads)
            return Result.Failure("Cannot delete zone: it has loads assigned. Remove or reassign loads first.");

        // Audit Log Entry ကို Delete မလုပ်မီ Audit ထဲသိမ်းရန် အချက်အလက်ယူဆောက်ထားခြင်း
        var auditEntry = new AuditEntryDto
        {
            FacilityId = facilityId,
            EventType = "DELETE_ZONE",
            Rationale = $"Deleted zone '{zone.Name}' (Type: {zone.Type})"
        };

        await _auditLogRepository.AddAsync(auditEntry, ct);

        await _repository.DeleteAsync(zoneId, facilityId, ct);

        return Result.Success();
    }
}