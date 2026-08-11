using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Zones;

public class ListZonesUseCase
{
    private readonly IZoneRepository _repository;

    public ListZonesUseCase(IZoneRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<ZoneDto>>> ExecuteAsync(Guid facilityId, CancellationToken ct = default)
    {
        var zones = await _repository.GetAllByFacilityAsync(facilityId, ct);

        var tree = BuildTree(zones);

        return Result<List<ZoneDto>>.Success(tree);
    }

    private static List<ZoneDto> BuildTree(List<ZoneDto> zones)
    {
        var lookup = zones.ToDictionary(z => z.Id);
        var roots = new List<ZoneDto>();

        foreach (var zone in zones)
        {
            if (zone.ParentZoneId.HasValue && lookup.TryGetValue(zone.ParentZoneId.Value, out var parent))
            {
                parent.Children.Add(zone);
            }
            else
            {
                roots.Add(zone);
            }
        }

        return roots;
    }
}
