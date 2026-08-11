using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class DeleteZoneUseCase
{
    private readonly IZoneRepository _repository;

    public DeleteZoneUseCase(IZoneRepository repository)
    {
        _repository = repository;
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

        await _repository.DeleteAsync(zoneId, facilityId, ct);
        return Result.Success();
    }
}
