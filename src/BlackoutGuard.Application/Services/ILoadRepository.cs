using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface ILoadRepository
{
    Task<LoadDto?> GetByRelayAddressAsync(Guid facilityId, int relayAddress, CancellationToken ct = default);
    Task<List<LoadDto>> GetP1LoadsAsync(Guid facilityId, CancellationToken ct = default);
    Task<Guid> AddAsync(LoadDto load, CancellationToken ct = default);
}
