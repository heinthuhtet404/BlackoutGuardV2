using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Loads;

public class ListLoadsUseCase
{
    private readonly ILoadRepository _loadRepo;

    public ListLoadsUseCase(ILoadRepository loadRepo)
    {
        _loadRepo = loadRepo;
    }

    public async Task<Result<List<LoadDto>>> ExecuteAsync(Guid facilityId, Guid? zoneId = null, CancellationToken ct = default)
    {
        var loads = await _loadRepo.GetAllByFacilityAsync(facilityId, zoneId, ct);
        return Result<List<LoadDto>>.Success(loads);
    }
}
