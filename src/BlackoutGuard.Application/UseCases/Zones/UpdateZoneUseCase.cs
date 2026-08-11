using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class UpdateZoneUseCase
{
    private readonly IZoneRepository _repository;

    public UpdateZoneUseCase(IZoneRepository repository)
    {
        _repository = repository;
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
        return Result.Success();
    }
}
