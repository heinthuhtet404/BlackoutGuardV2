using System.Data;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;

namespace BlackoutGuard.Application.Tests.UseCases.Loads;

public class DeleteLoadUseCaseTests
{
    [Fact]
    public async Task Execute_ShouldDeleteLoad_WithCascadeCleanup()
    {
        var facilityId = Guid.NewGuid();
        var fakes = new Fakes();
        var load = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ZoneId = Guid.NewGuid(),
            Name = "Generator Shed Load",
            RelayAddress = 7,
            PowerRatingKw = 45,
            Priority = "P2",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(load);

        var useCase = fakes.BuildUseCase();

        var result = await useCase.ExecuteAsync(load.Id, facilityId);

        Assert.True(result.IsSuccess);
        Assert.True(fakes.TxCommitted);
        Assert.Empty(fakes.LoadRepo.Loads);
    }

    [Fact]
    public async Task Execute_ShouldWriteAuditEntry_WithCorrectEventTypeAndRationale()
    {
        var facilityId = Guid.NewGuid();
        var fakes = new Fakes();
        var load = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ZoneId = Guid.NewGuid(),
            Name = "ICU Ventilator Bank",
            RelayAddress = 3,
            PowerRatingKw = 120,
            Priority = "P1",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(load);

        var useCase = fakes.BuildUseCase();

        var result = await useCase.ExecuteAsync(load.Id, facilityId);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(fakes.AuditRepo.Entries);
        Assert.Equal("LoadDeleted", entry.EventType);
        Assert.Contains("ICU Ventilator Bank", entry.Rationale);
        Assert.Contains("P1", entry.Rationale);
        Assert.Contains(facilityId.ToString(), entry.Rationale);
        Assert.Equal(load.Id, entry.AffectedLoadId);
        Assert.Equal(facilityId, entry.FacilityId);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenLoadNotFound()
    {
        var fakes = new Fakes();
        var useCase = fakes.BuildUseCase();

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
        Assert.Empty(fakes.AuditRepo.Entries);
    }

    [Fact]
    public async Task Execute_ShouldFail_WhenLoadBelongsToDifferentFacility()
    {
        var facilityA = Guid.NewGuid();
        var facilityB = Guid.NewGuid();
        var fakes = new Fakes();
        var load = new LoadDto
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityA,
            ZoneId = Guid.NewGuid(),
            Name = "Other Facility Load",
            RelayAddress = 5,
            PowerRatingKw = 30,
            Priority = "P3",
            IsActive = true
        };
        fakes.LoadRepo.Loads.Add(load);

        var useCase = fakes.BuildUseCase();

        var result = await useCase.ExecuteAsync(load.Id, facilityB);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.False(fakes.TxCommitted);
        Assert.Single(fakes.LoadRepo.Loads);
        Assert.Empty(fakes.AuditRepo.Entries);
    }

    private sealed class Fakes
    {
        public FakeLoadRepository LoadRepo { get; } = new();
        public FakeAuditRepository AuditRepo { get; } = new();
        public FakeTxFactory TxFactory { get; } = new();

        public bool TxCommitted => TxFactory.CurrentTx?.Committed ?? false;

        public DeleteLoadUseCase BuildUseCase()
        {
            var executionStrategy = new FakeExecutionStrategy();
            return new DeleteLoadUseCase(LoadRepo, AuditRepo, TxFactory, executionStrategy);
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
