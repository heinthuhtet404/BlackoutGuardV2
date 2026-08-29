using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class UpdateZoneUseCase
{
    private readonly IZoneRepository _repository;
    private readonly IDecisionAuditLogRepository _auditLogRepository;

    public UpdateZoneUseCase(
        IZoneRepository repository,
        IDecisionAuditLogRepository auditLogRepository)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result> ExecuteAsync(
        Guid zoneId,
        Guid facilityId,
        string? name = null,
        string? type = null,
        Guid? parentZoneId = null,
        CancellationToken ct = default)
    {
        var zone = await _repository.GetByIdAsync(zoneId, facilityId, ct);
        if (zone is null)
            return Result.Failure("Zone not found in this facility.");

        if (parentZoneId.HasValue)
        {
            if (parentZoneId.Value == zoneId)
                return Result.Failure("A zone cannot be its own parent.");

            var parentExists = await _repository.ExistsInFacilityAsync(parentZoneId.Value, facilityId, ct);
            if (!parentExists)
                return Result.Failure("Parent zone does not exist in this facility.");

            var ancestors = await _repository.GetAncestorIdsAsync(parentZoneId.Value, ct);
            if (ancestors.Contains(zoneId))
                return Result.Failure("Cycle detected: the new parent is already a descendant of this zone.");
        }

        var oldName = zone.Name;
        var oldType = zone.Type;
        var oldParentId = zone.ParentZoneId;

        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result.Failure("Zone name is required.");
            zone.Name = name;
        }

        if (type is not null)
        {
            if (string.IsNullOrWhiteSpace(type))
                return Result.Failure("Zone type is required.");
            zone.Type = type;
        }

        zone.ParentZoneId = parentZoneId;

        await _repository.UpdateAsync(zone, ct);

        // Audit Log Entry
        var auditEntry = new AuditEntryDto
        {
            FacilityId = facilityId,
            EventType = "UPDATE_ZONE",
            Rationale = $"Updated zone '{zone.Name}'. " +
                        $"Name: '{oldName}' -> '{zone.Name}', " +
                        $"Type: '{oldType}' -> '{zone.Type}', " +
                        $"ParentZoneId: '{oldParentId}' -> '{zone.ParentZoneId}'"
        };

        await _auditLogRepository.AddAsync(auditEntry, ct);

        return Result.Success();
    }
}