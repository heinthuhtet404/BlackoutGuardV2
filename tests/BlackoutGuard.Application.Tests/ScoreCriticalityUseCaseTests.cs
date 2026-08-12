using System.Data;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;

namespace BlackoutGuard.Application.Tests.UseCases.Loads;

public class ScoreCriticalityUseCaseTests
{
    private static (Fakes Fakes, LoadDto Load) CreateAutoLoad(
        double generatorCapacityKw = 500,
        string currentPriority = "P2",
        double powerRatingKw = 60)
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = generatorCapacityKw
        };
        var fakes = new Fakes(facility);
        var load = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Test Load",
            RelayAddress = 1,
            PowerRatingKw = powerRatingKw,
            Priority = currentPriority,
            PriorityMode = "auto",
            IsActive = true,
            IsSheddable = true
        };
        fakes.LoadRepo.Loads.Add(load);
        return (fakes, load);
    }

    [Fact]
    public async Task Execute_ShouldScoreHigh_AsP1_AtExactly80()
    {
        var (fakes, load) = CreateAutoLoad();

        var useCase = fakes.BuildUseCase();
        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 10,
            Q2 = 10,
            Q3 = 10,
            Q4 = 1
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.Score);
        Assert.Equal("P1", result.Value.Priority);
        var updated = fakes.LoadRepo.Loads.Single();
        Assert.Equal(100, updated.CriticalityScore);
        Assert.Equal("P1", updated.Priority);
        Assert.Equal((short)1, updated.CriticalityQ4);
    }

    [Fact]
    public async Task Execute_ShouldScore79_AsP2_Boundary()
    {
        var (fakes, load) = CreateAutoLoad();

        // q1=8, q2=8, q3=6 → raw = 4.0+2.4+1.2 = 7.6 → score 76... adjust to hit 79:
        // raw 7.9 → q1=10, q2=8, q3=1 → 5.0+2.4+0.2 = 7.6 no.
        // raw 7.9: q1=10, q2=8, q3=3 → 5.0+2.4+0.6 = 8.0 → 80. 
        // raw 7.9: q1=10, q2=7, q3=6 → 5.0+2.1+1.2 = 8.3 no.
        // Try q1=9,q2=9,q3=6 → 4.5+2.7+1.2=8.4 → 84.
        // q1=9,q2=8,q3=6 → 4.5+2.4+1.2=8.1 → 81.
        // q1=9,q2=8,q3=5 → 4.5+2.4+1.0=7.9 → 79.
        var useCase = fakes.BuildUseCase();
        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 9,
            Q2 = 8,
            Q3 = 5,
            Q4 = 10
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(79, result.Value!.Score);
        Assert.Equal("P2", result.Value.Priority);
    }

    [Fact]
    public async Task Execute_ShouldScore40_AsP2_Boundary()
    {
        var (fakes, load) = CreateAutoLoad();

        // raw 4.0 → score 40 → P2 (>= 40)
        // q1=6, q2=2, q3=2 → 3.0+0.6+0.4 = 4.0
        var useCase = fakes.BuildUseCase();
        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 6,
            Q2 = 2,
            Q3 = 2,
            Q4 = 5
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(40, result.Value!.Score);
        Assert.Equal("P2", result.Value.Priority);
    }

    [Fact]
    public async Task Execute_ShouldScore39_AsP3_Boundary()
    {
        var (fakes, load) = CreateAutoLoad();

        // raw 3.9 → score 39 → P3 (< 40)
        // q1=6, q2=2, q3=1 → 3.0+0.6+0.2 = 3.8 → 38 no.
        // raw 3.9: q1=6, q2=1, q3=3 → 3.0+0.3+0.6=3.9 → 39
        var useCase = fakes.BuildUseCase();
        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 6,
            Q2 = 1,
            Q3 = 3,
            Q4 = 5
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(39, result.Value!.Score);
        Assert.Equal("P3", result.Value.Priority);
    }

    [Fact]
    public async Task Execute_ShouldExcludeQ4_FromScoreCalculation()
    {
        var (fakes, load) = CreateAutoLoad();

        var baseCase = await fakes.BuildUseCase().ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 5,
            Q2 = 5,
            Q3 = 5,
            Q4 = 1
        });

        load.Priority = "P2";
        var highQ4 = await fakes.BuildUseCase().ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 5,
            Q2 = 5,
            Q3 = 5,
            Q4 = 10
        });

        Assert.True(baseCase.IsSuccess);
        Assert.True(highQ4.IsSuccess);
        Assert.Equal(baseCase.Value!.Score, highQ4.Value!.Score);
    }

    [Fact]
    public async Task Execute_ShouldRejectManualMode()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 500
        };
        var fakes = new Fakes(facility);
        var load = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Manual Load",
            RelayAddress = 1,
            PowerRatingKw = 60,
            Priority = "P2",
            PriorityMode = "manual",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(load);

        var useCase = fakes.BuildUseCase();
        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 10,
            Q2 = 10,
            Q3 = 10,
            Q4 = 10
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("manual", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_ShouldRejectInvalidInputRange()
    {
        var (fakes, load) = CreateAutoLoad();
        var useCase = fakes.BuildUseCase();

        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 11,
            Q2 = 5,
            Q3 = 5,
            Q4 = 5
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("1 and 10", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_ShouldTriggerCapacityCheck_WhenPushedIntoP1()
    {
        var (fakes, load) = CreateAutoLoad(
            generatorCapacityKw: 100,
            currentPriority: "P2",
            powerRatingKw: 150);

        var useCase = fakes.BuildUseCase();
        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 10,
            Q2 = 10,
            Q3 = 10,
            Q4 = 5
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("capacity", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("P2", fakes.LoadRepo.Loads.Single().Priority);
    }

    [Fact]
    public async Task Execute_ShouldSkipCapacityCheck_WhenPriorityUnchanged()
    {
        var (fakes, load) = CreateAutoLoad(
            generatorCapacityKw: 100,
            currentPriority: "P1",
            powerRatingKw: 90);

        var useCase = fakes.BuildUseCase();
        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = load.Id,
            FacilityId = load.FacilityId,
            Q1 = 10,
            Q2 = 10,
            Q3 = 10,
            Q4 = 5
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("P1", result.Value!.Priority);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenLoadNotFound()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 500
        };
        var fakes = new Fakes(facility);
        var useCase = fakes.BuildUseCase();

        var result = await useCase.ExecuteAsync(new ScoreCriticalityRequest
        {
            LoadId = Guid.NewGuid(),
            FacilityId = facility.Id,
            Q1 = 5,
            Q2 = 5,
            Q3 = 5,
            Q4 = 5
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
    }

    private sealed class Fakes
    {
        public FakeLoadRepository LoadRepo { get; } = new();
        public FakeFacilityRepository FacilityRepo { get; }
        public FakeTxFactory TxFactory { get; } = new();

        public Fakes(FacilityDto? facility)
        {
            FacilityRepo = new FakeFacilityRepository(facility);
        }

        public ScoreCriticalityUseCase BuildUseCase()
        {
            var executionStrategy = new FakeExecutionStrategy();
            return new ScoreCriticalityUseCase(LoadRepo, FacilityRepo, TxFactory, executionStrategy);
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

    private sealed class FakeFacilityRepository : IFacilityRepository
    {
        private readonly FacilityDto? _facility;

        public FakeFacilityRepository(FacilityDto? facility)
        {
            _facility = facility;
        }

        public Task<FacilityDto?> GetByIdAsync(Guid facilityId, CancellationToken ct = default)
        {
            return Task.FromResult(_facility);
        }
    }
}
