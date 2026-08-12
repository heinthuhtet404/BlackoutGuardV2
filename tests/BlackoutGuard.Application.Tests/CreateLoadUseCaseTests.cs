using System.Data;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;

namespace BlackoutGuard.Application.Tests.UseCases.Loads;

public class CreateLoadUseCaseTests
{
    [Fact]
    public async Task Execute_ShouldCreateLoad_WithValidRequest()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 500
        };

        var fakes = new Fakes(facility);
        var useCase = fakes.BuildUseCase();

        var request = new CreateLoadRequest
        {
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Test Load",
            RelayAddress = 1,
            PowerRatingKw = 100,
            Priority = "P2"
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        var saved = fakes.LoadRepo.Loads.Single();
        Assert.Equal("Test Load", saved.Name);
        Assert.Equal(1, saved.RelayAddress);
        Assert.True(fakes.TxCommitted);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenRelayAddressConflicts()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 500
        };

        var fakes = new Fakes(facility);
        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            Name = "Existing ICU Load",
            RelayAddress = 3,
            PowerRatingKw = 50,
            Priority = "P1"
        });

        var useCase = fakes.BuildUseCase();

        var request = new CreateLoadRequest
        {
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "New Load",
            RelayAddress = 3,
            PowerRatingKw = 100,
            Priority = "P2"
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("Relay address 3", result.ErrorMessage);
        Assert.Contains("Existing ICU Load", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenP1ExceedsCapacityWithoutForce()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 100
        };

        var fakes = new Fakes(facility);
        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            Name = "Existing P1",
            RelayAddress = 1,
            PowerRatingKw = 80,
            Priority = "P1",
            IsActive = true
        });

        var useCase = fakes.BuildUseCase();

        var request = new CreateLoadRequest
        {
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "New P1 Load",
            RelayAddress = 2,
            PowerRatingKw = 50,
            Priority = "P1"
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("30.0 kW", result.ErrorMessage);
        Assert.Contains("130.0 kW", result.ErrorMessage);
        Assert.Contains("100.0 kW", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
    }

    [Fact]
    public async Task Execute_ShouldSucceed_WhenP1ExceedsCapacityWithForce()
    {
        var facility = new FacilityDto
        {
            Id = Guid.NewGuid(),
            Name = "Test Facility",
            GeneratorCapacityKW = 100
        };

        var fakes = new Fakes(facility);
        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            Name = "Existing P1",
            RelayAddress = 1,
            PowerRatingKw = 80,
            Priority = "P1",
            IsActive = true
        });

        var useCase = fakes.BuildUseCase();

        var request = new CreateLoadRequest
        {
            FacilityId = facility.Id,
            ZoneId = Guid.NewGuid(),
            Name = "Forced P1 Load",
            RelayAddress = 2,
            PowerRatingKw = 50,
            Priority = "P1",
            Force = true
        };

        var result = await useCase.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
        Assert.Single(fakes.AuditRepo.Entries);
        var auditEntry = fakes.AuditRepo.Entries[0];
        Assert.Equal("CAPACITY_OVERRIDE", auditEntry.EventType);
        Assert.Contains("force=true", auditEntry.Rationale);
        Assert.Contains("130.0 kW", auditEntry.Rationale);
        Assert.Equal(result.Value, auditEntry.AffectedLoadId);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenFacilityNotFound()
    {
        var fakes = new Fakes(null);
        var useCase = fakes.BuildUseCase();

        var request = new CreateLoadRequest
        {
            FacilityId = Guid.NewGuid(),
            ZoneId = Guid.NewGuid(),
            Name = "Load",
            RelayAddress = 1,
            PowerRatingKw = 50,
            Priority = "P1"
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

        public CreateLoadUseCase BuildUseCase()
        {
            // ✅ IExecutionStrategy Mock ကို ဖန်တီးပါ
            var executionStrategy = new FakeExecutionStrategy();
            return new CreateLoadUseCase(LoadRepo, FacilityRepo, AuditRepo, TxFactory, executionStrategy);
        }
    }

    // ✅ IExecutionStrategy Implementation (Fake)
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