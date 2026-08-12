using System.Data;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;

namespace BlackoutGuard.Application.Tests.UseCases.Loads;

public class UpdateLoadUseCaseTests
{
    [Fact]
    public async Task Execute_ShouldSucceed_WhenNoSafetyRelevantFieldsChange()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 100
        };

        var fakes = new Fakes(facility);
        var load = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Original Name",
            RelayAddress = 1,
            PowerRatingKw = 30,
            Priority = "P2",
            PriorityMode = "auto",
            IsSheddable = true,
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(load);

        var useCase = fakes.BuildUpdateUseCase();

        var request = new UpdateLoadRequest
        {
            LoadId = load.Id,
            FacilityId = facility.Id,
            Name = "Renamed Load"
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
        var updated = fakes.LoadRepo.Loads.Single();
        Assert.Equal("Renamed Load", updated.Name);
        Assert.Equal(1, updated.RelayAddress);
        Assert.Equal("P2", updated.Priority);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenRelayAddressConflictsWithAnotherLoad()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 500
        };

        var fakes = new Fakes(facility);
        var targetLoad = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Target Load",
            RelayAddress = 1,
            PowerRatingKw = 30,
            Priority = "P2",
            IsActive = true
        };
        var otherLoad = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "ICU Ventilator Bank",
            RelayAddress = 3,
            PowerRatingKw = 50,
            Priority = "P1",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(targetLoad);
        fakes.LoadRepo.Loads.Add(otherLoad);

        var useCase = fakes.BuildUpdateUseCase();

        var request = new UpdateLoadRequest
        {
            LoadId = targetLoad.Id,
            FacilityId = facility.Id,
            RelayAddress = 3
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("Relay address 3", result.ErrorMessage);
        Assert.Contains("ICU Ventilator Bank", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Execute_ShouldNotFlagConflict_WhenAddressIsItsOwnCurrentAddress()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 500
        };

        var fakes = new Fakes(facility);
        var targetLoad = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Target Load",
            RelayAddress = 1,
            PowerRatingKw = 30,
            Priority = "P2",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(targetLoad);

        var useCase = fakes.BuildUpdateUseCase();

        var request = new UpdateLoadRequest
        {
            LoadId = targetLoad.Id,
            FacilityId = facility.Id,
            Name = "Updated Name",
            RelayAddress = 1
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenPriorityUpgradedToP1AndCapacityExceeded()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 100
        };

        var fakes = new Fakes(facility);
        var targetLoad = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Target Load",
            RelayAddress = 1,
            PowerRatingKw = 60,
            Priority = "P2",
            IsActive = true
        };
        var existingP1 = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Existing P1",
            RelayAddress = 2,
            PowerRatingKw = 80,
            Priority = "P1",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(targetLoad);
        fakes.LoadRepo.Loads.Add(existingP1);

        var useCase = fakes.BuildUpdateUseCase();

        var request = new UpdateLoadRequest
        {
            LoadId = targetLoad.Id,
            FacilityId = facility.Id,
            Priority = "P1"
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("40.0 kW", result.ErrorMessage);
        Assert.Contains("140.0 kW", result.ErrorMessage);
        Assert.Contains("100.0 kW", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenP1PowerRatingIncreasedBeyondCapacity()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 100
        };

        var fakes = new Fakes(facility);
        var targetLoad = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Target P1",
            RelayAddress = 1,
            PowerRatingKw = 60,
            Priority = "P1",
            IsActive = true
        };
        var otherP1 = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Other P1",
            RelayAddress = 2,
            PowerRatingKw = 20,
            Priority = "P1",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(targetLoad);
        fakes.LoadRepo.Loads.Add(otherP1);

        var useCase = fakes.BuildUpdateUseCase();

        var request = new UpdateLoadRequest
        {
            LoadId = targetLoad.Id,
            FacilityId = facility.Id,
            PowerRatingKw = 90
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("10.0 kW", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Execute_ShouldSkipCapacityCheck_WhenDowngradingFromP1()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 100
        };

        var fakes = new Fakes(facility);
        var targetLoad = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Target P1",
            RelayAddress = 1,
            PowerRatingKw = 80,
            Priority = "P1",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(targetLoad);

        var useCase = fakes.BuildUpdateUseCase();

        var request = new UpdateLoadRequest
        {
            LoadId = targetLoad.Id,
            FacilityId = facility.Id,
            Priority = "P3"
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
        var updated = fakes.LoadRepo.Loads.Single();
        Assert.Equal("P3", updated.Priority);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenLoadNotFound()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 100
        };

        var fakes = new Fakes(facility);
        var useCase = fakes.BuildUpdateUseCase();

        var request = new UpdateLoadRequest
        {
            LoadId = Guid.NewGuid(),
            FacilityId = facility.Id,
            Name = "Ghost"
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
    }

    private sealed class Fakes
    {
        public FakeLoadRepository LoadRepo { get; } = new();
        public FakeFacilityRepository FacilityRepo { get; }
        public FakeAuditRepository AuditRepo { get; } = new();
        public FakeTxFactory TxFactory { get; } = new();

        public bool TxCommitted => TxFactory.CurrentTx?.Committed ?? false;

        public Fakes(FacilityDto? facility)
        {
            FacilityRepo = new FakeFacilityRepository(facility);
        }

        public UpdateLoadUseCase BuildUpdateUseCase()
        {
            var executionStrategy = new FakeExecutionStrategy();
            return new UpdateLoadUseCase(LoadRepo, FacilityRepo, AuditRepo, TxFactory, executionStrategy);
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

    private sealed class FakeAuditRepository : IDecisionAuditLogRepository
    {
        public List<AuditEntryDto> Entries { get; } = new();

        public Task AddAsync(AuditEntryDto entry, CancellationToken ct = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
