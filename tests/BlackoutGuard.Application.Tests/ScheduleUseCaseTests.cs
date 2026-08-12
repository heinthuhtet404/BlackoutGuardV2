using System.Data;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Schedules;

namespace BlackoutGuard.Application.Tests.UseCases.Schedules;

public class ScheduleUseCaseTests
{
    private static (Fakes Fakes, LoadDto Load) CreateFakesWithLoad(Guid? facilityId = null)
    {
        var fakes = new Fakes();
        var load = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId ?? Guid.NewGuid(),
            ZoneId = Guid.NewGuid(),
            Name = "Chiller",
            RelayAddress = 1,
            PowerRatingKw = 50,
            Priority = "P2",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(load);
        return (fakes, load);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WithValidData()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = load.FacilityId,
            Name = "Evening Peak Shed",
            LoadId = load.Id,
            TargetPriority = "P2",
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(21, 0),
            DaysOfWeek = new short[] { 1, 2, 3, 4, 5 }
        });

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.True(fakes.TxCommitted);
        var created = fakes.ScheduleRepo.Schedules.Single();
        Assert.Equal("Evening Peak Shed", created.Name);
        Assert.Equal(5, created.DaysOfWeek.Length);
    }

    [Fact]
    public async Task Create_ShouldAllowOvernightWrappingSchedule()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = load.FacilityId,
            Name = "Night Mode",
            LoadId = load.Id,
            TargetPriority = "P3",
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(6, 0),
            DaysOfWeek = new short[] { 6, 7 }
        });

        Assert.True(result.IsSuccess);
        var created = fakes.ScheduleRepo.Schedules.Single();
        Assert.Equal(new TimeOnly(18, 0), created.StartTime);
        Assert.Equal(new TimeOnly(6, 0), created.EndTime);
    }

    [Fact]
    public async Task Create_ShouldReject_LoadFromDifferentFacility()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var otherFacility = Guid.NewGuid();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = otherFacility,
            Name = "Cross Facility",
            LoadId = load.Id,
            TargetPriority = "P2",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.Empty(fakes.ScheduleRepo.Schedules);
    }

    [Fact]
    public async Task Create_ShouldReject_InvalidDaysOfWeekValue()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = load.FacilityId,
            Name = "Bad Days",
            LoadId = load.Id,
            TargetPriority = "P2",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1, 8 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("1", result.ErrorMessage);
        Assert.Contains("7", result.ErrorMessage);
    }

    [Fact]
    public async Task Create_ShouldReject_ZeroDaysValue()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = load.FacilityId,
            Name = "Zero Day",
            LoadId = load.Id,
            TargetPriority = "P2",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 0 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("1", result.ErrorMessage);
    }

    [Fact]
    public async Task Create_ShouldReject_DuplicateDays()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = load.FacilityId,
            Name = "Duplicate Days",
            LoadId = load.Id,
            TargetPriority = "P2",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1, 1, 2 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("duplicate", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ShouldReject_EmptyDaysOfWeek()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = load.FacilityId,
            Name = "No Days",
            LoadId = load.Id,
            TargetPriority = "P2",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = Array.Empty<short>()
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("at least one day", result.ErrorMessage);
    }

    [Fact]
    public async Task Create_ShouldReject_InvalidTargetPriority()
    {
        var (fakes, load) = CreateFakesWithLoad();
        var useCase = fakes.BuildCreateUseCase();

        var result = await useCase.ExecuteAsync(new CreateScheduleRequest
        {
            FacilityId = load.FacilityId,
            Name = "Bad Priority",
            LoadId = load.Id,
            TargetPriority = "P9",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1 }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("P9", result.ErrorMessage);
    }

    [Fact]
    public async Task Delete_ShouldSucceed()
    {
        var fakes = new Fakes();
        var facilityId = Guid.NewGuid();
        var schedule = new ScheduleDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Name = "Delete Me",
            LoadId = Guid.NewGuid(),
            TargetPriority = "P2",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1 }
        };
        fakes.ScheduleRepo.Schedules.Add(schedule);

        var useCase = fakes.BuildDeleteUseCase();
        var result = await useCase.ExecuteAsync(schedule.Id, facilityId);

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
        Assert.Empty(fakes.ScheduleRepo.Schedules);
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenNotFound()
    {
        var fakes = new Fakes();
        var useCase = fakes.BuildDeleteUseCase();

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenDifferentFacility()
    {
        var fakes = new Fakes();
        var facilityA = Guid.NewGuid();
        var schedule = new ScheduleDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityA,
            Name = "Wrong Facility",
            LoadId = Guid.NewGuid(),
            TargetPriority = "P2",
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0),
            DaysOfWeek = new short[] { 1 }
        };
        fakes.ScheduleRepo.Schedules.Add(schedule);

        var useCase = fakes.BuildDeleteUseCase();
        var result = await useCase.ExecuteAsync(schedule.Id, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.Single(fakes.ScheduleRepo.Schedules);
    }

    private sealed class Fakes
    {
        public FakeScheduleRepository ScheduleRepo { get; } = new();
        public FakeLoadRepository LoadRepo { get; } = new();
        public FakeTxFactory TxFactory { get; } = new();

        public bool TxCommitted => TxFactory.CurrentTx?.Committed ?? false;

        public CreateScheduleUseCase BuildCreateUseCase()
        {
            var executionStrategy = new FakeExecutionStrategy();
            return new CreateScheduleUseCase(ScheduleRepo, LoadRepo, TxFactory, executionStrategy);
        }

        public DeleteScheduleUseCase BuildDeleteUseCase()
        {
            var executionStrategy = new FakeExecutionStrategy();
            return new DeleteScheduleUseCase(ScheduleRepo, TxFactory, executionStrategy);
        }
    }

    private sealed class FakeExecutionStrategy : IExecutionStrategy
    {
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
        {
            return await operation();
        }
    }

    private sealed class FakeTxFactory : IDbTransactionFactory
    {
        public FakeTransaction? CurrentTx { get; private set; }

        public Task<IDataTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct = default)
        {
            CurrentTx = new FakeTransaction();
            return Task.FromResult<IDataTransaction>(CurrentTx);
        }
    }

    private sealed class FakeTransaction : IDataTransaction
    {
        public bool Committed { get; private set; }

        public Task CommitAsync(CancellationToken ct = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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

    private sealed class FakeLoadRepository : ILoadRepository
    {
        public List<LoadDto> Loads { get; } = new();

        public Task<LoadDto?> GetByRelayAddressAsync(Guid facilityId, int relayAddress, Guid? excludeLoadId = null, CancellationToken ct = default)
        {
            var match = Loads.FirstOrDefault(l => l.FacilityId == facilityId && l.RelayAddress == relayAddress
                && (!excludeLoadId.HasValue || l.Id != excludeLoadId.Value));
            return Task.FromResult(match);
        }

        public Task<LoadDto?> GetByIdAsync(Guid loadId, Guid facilityId, CancellationToken ct = default)
        {
            var match = Loads.FirstOrDefault(l => l.Id == loadId && l.FacilityId == facilityId);
            return Task.FromResult(match);
        }

        public Task<List<LoadDto>> GetP1LoadsAsync(Guid facilityId, Guid? excludeLoadId = null, CancellationToken ct = default)
        {
            var p1Loads = Loads.Where(l => l.FacilityId == facilityId && l.Priority == "P1" && l.IsActive
                && (!excludeLoadId.HasValue || l.Id != excludeLoadId.Value)).ToList();
            return Task.FromResult(p1Loads);
        }

        public Task<List<LoadDto>> GetAllByFacilityAsync(Guid facilityId, Guid? zoneId = null, CancellationToken ct = default)
        {
            var loads = Loads.Where(l => l.FacilityId == facilityId
                && (!zoneId.HasValue || l.ZoneId == zoneId.Value)).ToList();
            return Task.FromResult(loads);
        }

        public Task<Guid> AddAsync(LoadDto load, CancellationToken ct = default)
        {
            Loads.Add(load);
            return Task.FromResult(load.Id);
        }

        public Task UpdateAsync(LoadDto load, CancellationToken ct = default)
        {
            var index = Loads.FindIndex(l => l.Id == load.Id && l.FacilityId == load.FacilityId);
            if (index >= 0)
                Loads[index] = load;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid loadId, Guid facilityId, CancellationToken ct = default)
        {
            Loads.RemoveAll(l => l.Id == loadId && l.FacilityId == facilityId);
            return Task.CompletedTask;
        }
    }
}
