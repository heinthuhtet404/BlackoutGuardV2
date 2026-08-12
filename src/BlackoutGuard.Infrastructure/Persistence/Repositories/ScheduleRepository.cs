using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BlackoutGuard.Infrastructure.Persistence.Repositories;

public class ScheduleRepository : IScheduleRepository
{
    private readonly BlackoutGuardDbContext _context;

    public ScheduleRepository(BlackoutGuardDbContext context)
    {
        _context = context;
    }

    public async Task<ScheduleDto?> GetByIdAsync(Guid scheduleId, Guid facilityId, CancellationToken ct = default)
    {
        var schedule = await _context.TimeSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.FacilityId == facilityId, ct);

        return schedule is null ? null : MapToDto(schedule);
    }

    public async Task<Guid> CreateAsync(ScheduleDto schedule, CancellationToken ct = default)
    {
        var entity = new TimeSchedule
        {
            Id = schedule.Id,
            FacilityId = schedule.FacilityId,
            Name = schedule.Name,
            LoadId = schedule.LoadId,
            TargetPriority = schedule.TargetPriority,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            DaysOfWeek = schedule.DaysOfWeek,
            IsActive = schedule.IsActive
        };

        _context.TimeSchedules.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task DeleteAsync(Guid scheduleId, Guid facilityId, CancellationToken ct = default)
    {
        var entity = await _context.TimeSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.FacilityId == facilityId, ct);

        if (entity is null)
            return;

        _context.TimeSchedules.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    private static ScheduleDto MapToDto(TimeSchedule schedule)
    {
        return new ScheduleDto
        {
            Id = schedule.Id,
            FacilityId = schedule.FacilityId,
            Name = schedule.Name,
            LoadId = schedule.LoadId,
            TargetPriority = schedule.TargetPriority,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            DaysOfWeek = schedule.DaysOfWeek,
            IsActive = schedule.IsActive
        };
    }
}
