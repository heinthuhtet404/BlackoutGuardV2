using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;

namespace BlackoutGuard.Application.UseCases.Schedules;

public class ListSchedulesUseCase
{
    private readonly IScheduleRepository _scheduleRepo;

    public ListSchedulesUseCase(IScheduleRepository scheduleRepo)
    {
        _scheduleRepo = scheduleRepo;
    }

    public async Task<Result<List<ScheduleDto>>> ExecuteAsync(Guid facilityId, CancellationToken ct = default)
    {
        var schedules = await _scheduleRepo.GetAllByFacilityAsync(facilityId, ct);
        return Result<List<ScheduleDto>>.Success(schedules);
    }
}
