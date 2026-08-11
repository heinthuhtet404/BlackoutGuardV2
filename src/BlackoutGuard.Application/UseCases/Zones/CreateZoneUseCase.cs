using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class CreateZoneUseCase
{
    private readonly IZoneRepository _repository;

    public CreateZoneUseCase(IZoneRepository repository)
    {
        _repository = repository;
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
        return Result<Guid>.Success(id);
    }
}
