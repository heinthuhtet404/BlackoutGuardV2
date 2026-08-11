using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface IZoneRepository
{
    Task<ZoneDto?> GetByIdAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default);
    Task<Guid> CreateAsync(ZoneDto zone, CancellationToken ct = default);
    Task UpdateAsync(ZoneDto zone, CancellationToken ct = default);
    Task DeleteAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default);
    Task<bool> HasChildrenAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default);
    Task<bool> HasLoadsAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default);
    Task<List<ZoneDto>> GetAllByFacilityAsync(Guid facilityId, CancellationToken ct = default);
    Task<bool> ExistsInFacilityAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default);
    Task<List<Guid>> GetAncestorIdsAsync(Guid zoneId, CancellationToken ct = default);
}
