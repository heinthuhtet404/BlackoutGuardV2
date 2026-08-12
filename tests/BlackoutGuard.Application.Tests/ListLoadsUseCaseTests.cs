using System.Data;
using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;

namespace BlackoutGuard.Application.Tests.UseCases.Loads;

public class ListLoadsUseCaseTests
{
    [Fact]
    public async Task Execute_ShouldReturnAllLoads_ForFacility()
    {
        var fakes = new Fakes();
        var facilityId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();

        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityId, ZoneId = zoneId,
            Name = "Load A", RelayAddress = 1, Priority = "P1", IsActive = true
        });
        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityId, ZoneId = zoneId,
            Name = "Load B", RelayAddress = 2, Priority = "P2", IsActive = true
        });

        var useCase = new ListLoadsUseCase(fakes.LoadRepo);
        var result = await useCase.ExecuteAsync(facilityId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task Execute_ShouldFilterByZoneId()
    {
        var fakes = new Fakes();
        var facilityId = Guid.NewGuid();
        var zoneA = Guid.NewGuid();
        var zoneB = Guid.NewGuid();

        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityId, ZoneId = zoneA,
            Name = "Zone A Load", RelayAddress = 1, Priority = "P1", IsActive = true
        });
        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityId, ZoneId = zoneB,
            Name = "Zone B Load", RelayAddress = 2, Priority = "P2", IsActive = true
        });

        var useCase = new ListLoadsUseCase(fakes.LoadRepo);
        var result = await useCase.ExecuteAsync(facilityId, zoneA);

        Assert.True(result.IsSuccess);
        var loads = result.Value!;
        Assert.Single(loads);
        Assert.Equal("Zone A Load", loads[0].Name);
    }

    [Fact]
    public async Task Execute_ShouldNotReturnOtherFacilityLoads()
    {
        var fakes = new Fakes();
        var facilityA = Guid.NewGuid();
        var facilityB = Guid.NewGuid();

        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityA, ZoneId = Guid.NewGuid(),
            Name = "Facility A Load", RelayAddress = 1, Priority = "P1", IsActive = true
        });
        fakes.LoadRepo.Loads.Add(new LoadDto
        {
            Id = Guid.NewGuid(), FacilityId = facilityB, ZoneId = Guid.NewGuid(),
            Name = "Facility B Load", RelayAddress = 2, Priority = "P2", IsActive = true
        });

        var useCase = new ListLoadsUseCase(fakes.LoadRepo);
        var result = await useCase.ExecuteAsync(facilityA);

        Assert.True(result.IsSuccess);
        var loads = result.Value!;
        Assert.Single(loads);
        Assert.Equal("Facility A Load", loads[0].Name);
    }

    private sealed class Fakes
    {
        public FakeLoadRepository LoadRepo { get; } = new();
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

        public Task<List<LoadDto>> GetAllByFacilityAsync(Guid facilityId, Guid? zoneId = null, CancellationToken ct = default)
        {
            var loads = Loads.Where(l => l.FacilityId == facilityId
                && (!zoneId.HasValue || l.ZoneId == zoneId.Value)).ToList();
            return Task.FromResult(loads);
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
}
