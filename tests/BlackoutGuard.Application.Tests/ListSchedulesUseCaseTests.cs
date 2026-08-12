using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Schedules;

namespace BlackoutGuard.Application.Tests.UseCases.Schedules;

public class ListSchedulesUseCaseTests
{
    [Fact]
    public async Task Execute_ShouldReturnAllSchedules_ForFacility()
    {
        var fakes = new Fakes();
        var facilityId = Guid.NewGuid();

        fakes.ScheduleRepo.Schedules.Add(new ScheduleDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Morning Shed",
            LoadId = Guid.NewGuid(), TargetPriority = "P2",
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1, 2, 3 }
        });
        fakes.ScheduleRepo.Schedules.Add(new ScheduleDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Night Mode",
            LoadId = Guid.NewGuid(), TargetPriority = "P3",
            StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(6, 0),
            DaysOfWeek = new short[] { 6, 7 }
        });

        var useCase = new ListSchedulesUseCase(fakes.ScheduleRepo);
        var result = await useCase.ExecuteAsync(facilityId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task Execute_ShouldNotReturnOtherFacilitySchedules()
    {
        var fakes = new Fakes();
        var facilityA = Guid.NewGuid();
        var facilityB = Guid.NewGuid();

        fakes.ScheduleRepo.Schedules.Add(new ScheduleDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityA, Name = "A Schedule",
            LoadId = Guid.NewGuid(), TargetPriority = "P2",
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1 }
        });
        fakes.ScheduleRepo.Schedules.Add(new ScheduleDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityB, Name = "B Schedule",
            LoadId = Guid.NewGuid(), TargetPriority = "P2",
            StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1 }
        });

        var useCase = new ListSchedulesUseCase(fakes.ScheduleRepo);
        var result = await useCase.ExecuteAsync(facilityA);

        Assert.True(result.IsSuccess);
        var schedules = result.Value!;
        Assert.Single(schedules);
        Assert.Equal("A Schedule", schedules[0].Name);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenNoSchedules()
    {
        var fakes = new Fakes();
        var useCase = new ListSchedulesUseCase(fakes.ScheduleRepo);

        var result = await useCase.ExecuteAsync(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    private sealed class Fakes
    {
        public FakeScheduleRepository ScheduleRepo { get; } = new();
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public List<ScheduleDto> Schedules { get; } = new();

        public Task<List<ScheduleDto>> GetAllByFacilityAsync(Guid facilityId, CancellationToken ct = default)
        {
            var schedules = Schedules.Where(s => s.FacilityId == facilityId).ToList();
            return Task.FromResult(schedules);
        }

        public Task<ScheduleDto?> GetByIdAsync(Guid scheduleId, Guid facilityId, CancellationToken ct = default)
        {
            var match = Schedules.FirstOrDefault(s => s.Id == scheduleId && s.FacilityId == facilityId);
            return Task.FromResult(match);
        }

        public Task<Guid> CreateAsync(ScheduleDto schedule, CancellationToken ct = default)
        {
            Schedules.Add(schedule);
            return Task.FromResult(schedule.Id);
        }

        public Task DeleteAsync(Guid scheduleId, Guid facilityId, CancellationToken ct = default)
        {
            Schedules.RemoveAll(s => s.Id == scheduleId && s.FacilityId == facilityId);
            return Task.CompletedTask;
        }
    }
}
