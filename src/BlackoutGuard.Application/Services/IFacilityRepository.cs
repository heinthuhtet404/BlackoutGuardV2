using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface IFacilityRepository
{
    Task<FacilityDto?> GetByIdAsync(Guid facilityId, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
