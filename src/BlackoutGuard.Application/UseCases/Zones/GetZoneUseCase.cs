using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class GetZoneUseCase
{
    private readonly IZoneRepository _zoneRepository;

    public GetZoneUseCase(IZoneRepository zoneRepository)
    {
        _zoneRepository = zoneRepository;
    }

    public async Task<Result<ZoneDto>> ExecuteAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
    {
        var zone = await _zoneRepository.GetByIdAsync(zoneId, facilityId, ct);
        if (zone is null)
            return Result<ZoneDto>.Failure($"Zone {zoneId} not found in this facility.");

        return Result<ZoneDto>.Success(zone);
    }
}