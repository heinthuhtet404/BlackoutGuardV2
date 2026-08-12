using BlackoutGuard.Application.DTOs;

namespace BlackoutGuard.Application.Services;

public interface IScheduleRepository
{
    Task<ScheduleDto?> GetByIdAsync(Guid scheduleId, Guid facilityId, CancellationToken ct = default);
    Task<Guid> CreateAsync(ScheduleDto schedule, CancellationToken ct = default);
    Task DeleteAsync(Guid scheduleId, Guid facilityId, CancellationToken ct = default);
}
