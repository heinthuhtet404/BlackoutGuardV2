using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface ILoadRepository
{
    Task<LoadDto?> GetByRelayAddressAsync(Guid facilityId, int relayAddress, Guid? excludeLoadId = null, CancellationToken ct = default);
    Task<LoadDto?> GetByIdAsync(Guid loadId, Guid facilityId, CancellationToken ct = default);
    Task<List<LoadDto>> GetAllByFacilityAsync(Guid facilityId, Guid? zoneId = null, CancellationToken ct = default);
    Task<List<LoadDto>> GetP1LoadsAsync(Guid facilityId, Guid? excludeLoadId = null, CancellationToken ct = default);
    Task<Guid> AddAsync(LoadDto load, CancellationToken ct = default);
    Task UpdateAsync(LoadDto load, CancellationToken ct = default);
    Task DeleteAsync(Guid loadId, Guid facilityId, CancellationToken ct = default);
}
