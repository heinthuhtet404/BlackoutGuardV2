using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class CreateZoneUseCase
{
    private readonly IZoneRepository _repository;
    private readonly IFacilityRepository _facilityRepository;
    private readonly IDecisionAuditLogRepository _auditLogRepository;

    public CreateZoneUseCase(
        IZoneRepository repository,
        IFacilityRepository facilityRepository,
        IDecisionAuditLogRepository auditLogRepository)
    {
        _repository = repository;
        _facilityRepository = facilityRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        Guid facilityId,
        string name,
        string type,
        Guid? parentZoneId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid>.Failure("Zone name is required.");

        if (string.IsNullOrWhiteSpace(type))
            return Result<Guid>.Failure("Zone type is required.");

        // ၁။ ပေးပို့လိုက်သော facilityId သည် DB ထဲတွင် အမှန်တကယ် ရှိမရှိ စစ်ဆေးခြင်း
        var facilityExists = await _facilityRepository.ExistsAsync(facilityId, ct);
        if (!facilityExists)
        {
            return Result<Guid>.Failure($"Facility with ID '{facilityId}' does not exist.");
        }

        // ၂။ Parent Zone ရှိပါက ထို Facility ၏ Zone ဖြစ်မဖြစ် စစ်ဆေးခြင်း
        if (parentZoneId.HasValue)
        {
            var parentExists = await _repository.ExistsInFacilityAsync(parentZoneId.Value, facilityId, ct);
            if (!parentExists)
                return Result<Guid>.Failure("Parent zone does not exist in this facility.");
        }

        var zone = new ZoneDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Name = name,
            Type = type,
            ParentZoneId = parentZoneId
        };

        var id = await _repository.CreateAsync(zone, ct);

        // Audit Log Entry
        var auditEntry = new AuditEntryDto
        {
            FacilityId = facilityId,
            EventType = "CREATE_ZONE",
            Rationale = $"Created zone '{name}' (Type: {type}, ParentZoneId: {parentZoneId})"
        };

        await _auditLogRepository.AddAsync(auditEntry, ct);

        return Result<Guid>.Success(id);
    }
}