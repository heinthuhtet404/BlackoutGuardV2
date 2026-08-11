using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Zones;

namespace BlackoutGuard.Application.Tests.UseCases.Zones;

public class ZoneUseCaseTests
{
    private static FakeZoneRepository CreateRepository() => new();

    [Fact]
    public async Task Create_ShouldSucceed_WithValidData()
    {
        var repo = CreateRepository();
        var useCase = new CreateZoneUseCase(repo);
        var facilityId = Guid.NewGuid();

        var result = await useCase.ExecuteAsync(facilityId, "Zone A", "building");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        var created = await repo.GetByIdAsync(result.Value, facilityId);
        Assert.NotNull(created);
        Assert.Equal("Zone A", created!.Name);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenNameIsEmpty()
    {
        var useCase = new CreateZoneUseCase(CreateRepository());

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), "", "building");

        Assert.False(result.IsSuccess);
        Assert.Contains("name", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ShouldFail_WhenParentNotInSameFacility()
    {
        var repo = CreateRepository();
        var facilityA = Guid.NewGuid();
        var facilityB = Guid.NewGuid();

        var parent = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityB, Name = "Parent", Type = "building" };
        await repo.CreateAsync(parent);

        var useCase = new CreateZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(facilityA, "Child", "room", parent.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("parent", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ShouldSucceed_WhenParentInSameFacility()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();

        var parent = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Parent", Type = "building" };
        await repo.CreateAsync(parent);

        var useCase = new CreateZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(facilityId, "Child", "room", parent.Id);

        Assert.True(result.IsSuccess);
        var child = await repo.GetByIdAsync(result.Value, facilityId);
        Assert.Equal(parent.Id, child!.ParentZoneId);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenZoneNotFound()
    {
        var useCase = new UpdateZoneUseCase(CreateRepository());

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), name: "New Name");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ShouldFail_WhenParentIsSelf()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();
        var zone = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Zone", Type = "building" };
        await repo.CreateAsync(zone);

        var useCase = new UpdateZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(zone.Id, facilityId, parentZoneId: zone.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("own parent", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ShouldDetectCycle()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();

        var root = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Root", Type = "building" };
        var child = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Child", Type = "room", ParentZoneId = root.Id };
        var grandchild = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Grandchild", Type = "room", ParentZoneId = child.Id };

        await repo.CreateAsync(root);
        await repo.CreateAsync(child);
        await repo.CreateAsync(grandchild);

        repo.SetAncestorChain(child.Id, new List<Guid> { root.Id });
        repo.SetAncestorChain(grandchild.Id, new List<Guid> { child.Id, root.Id });

        var useCase = new UpdateZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(root.Id, facilityId, parentZoneId: grandchild.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("cycle", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_ShouldSucceed_WithValidParentChange()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();

        var zone1 = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Zone1", Type = "building" };
        var zone2 = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Zone2", Type = "building" };
        await repo.CreateAsync(zone1);
        await repo.CreateAsync(zone2);

        repo.SetAncestorChain(zone2.Id, new List<Guid>());

        var useCase = new UpdateZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(zone1.Id, facilityId, parentZoneId: zone2.Id);

        Assert.True(result.IsSuccess);
        var updated = await repo.GetByIdAsync(zone1.Id, facilityId);
        Assert.Equal(zone2.Id, updated!.ParentZoneId);
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenZoneHasChildren()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();

        var parent = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Parent", Type = "building" };
        var child = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Child", Type = "room", ParentZoneId = parent.Id };
        await repo.CreateAsync(parent);
        await repo.CreateAsync(child);

        var useCase = new DeleteZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(parent.Id, facilityId);

        Assert.False(result.IsSuccess);
        Assert.Contains("child", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenZoneHasLoads()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();

        var zone = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Zone", Type = "building" };
        await repo.CreateAsync(zone);
        repo.AddLoadToZone(zone.Id, facilityId);

        var useCase = new DeleteZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(zone.Id, facilityId);

        Assert.False(result.IsSuccess);
        Assert.Contains("load", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_ShouldSucceed_WhenZoneHasNoChildrenOrLoads()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();

        var zone = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Zone", Type = "building" };
        await repo.CreateAsync(zone);

        var useCase = new DeleteZoneUseCase(repo);
        var result = await useCase.ExecuteAsync(zone.Id, facilityId);

        Assert.True(result.IsSuccess);
        var deleted = await repo.GetByIdAsync(zone.Id, facilityId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task List_ShouldReturnTreeStructure()
    {
        var repo = CreateRepository();
        var facilityId = Guid.NewGuid();

        var root = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Root", Type = "building" };
        var child1 = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Floor 1", Type = "floor", ParentZoneId = root.Id };
        var child2 = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Floor 2", Type = "floor", ParentZoneId = root.Id };
        var room = new ZoneDto { Id = Guid.NewGuid(), FacilityId = facilityId, Name = "Room 1A", Type = "room", ParentZoneId = child1.Id };

        await repo.CreateAsync(root);
        await repo.CreateAsync(child1);
        await repo.CreateAsync(child2);
        await repo.CreateAsync(room);

        var useCase = new ListZonesUseCase(repo);
        var result = await useCase.ExecuteAsync(facilityId);

        Assert.True(result.IsSuccess);
        var tree = result.Value!;
        Assert.Single(tree);
        Assert.Equal("Root", tree[0].Name);
        Assert.Equal(2, tree[0].Children.Count);
        Assert.Single(tree[0].Children[0].Children);
        Assert.Equal("Room 1A", tree[0].Children[0].Children[0].Name);
    }

    private sealed class FakeZoneRepository : IZoneRepository
    {
        private readonly List<ZoneDto> _zones = new();
        private readonly HashSet<(Guid ZoneId, Guid FacilityId)> _zoneLoads = new();
        private readonly Dictionary<Guid, List<Guid>> _ancestorChains = new();

        public void SetAncestorChain(Guid zoneId, List<Guid> ancestors)
        {
            _ancestorChains[zoneId] = ancestors;
        }

        public void AddLoadToZone(Guid zoneId, Guid facilityId)
        {
            _zoneLoads.Add((zoneId, facilityId));
        }

        public Task<ZoneDto?> GetByIdAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
        {
            var zone = _zones.FirstOrDefault(z => z.Id == zoneId && z.FacilityId == facilityId);
            var clone = zone is null ? null : Clone(zone);
            return Task.FromResult(clone);
        }

        public Task<Guid> CreateAsync(ZoneDto zone, CancellationToken ct = default)
        {
            _zones.Add(Clone(zone));
            return Task.FromResult(zone.Id);
        }

        public Task UpdateAsync(ZoneDto zone, CancellationToken ct = default)
        {
            var index = _zones.FindIndex(z => z.Id == zone.Id);
            if (index >= 0)
                _zones[index] = Clone(zone);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
        {
            _zones.RemoveAll(z => z.Id == zoneId && z.FacilityId == facilityId);
            return Task.CompletedTask;
        }

        public Task<bool> HasChildrenAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
        {
            var result = _zones.Any(z => z.ParentZoneId == zoneId && z.FacilityId == facilityId);
            return Task.FromResult(result);
        }

        public Task<bool> HasLoadsAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
        {
            return Task.FromResult(_zoneLoads.Contains((zoneId, facilityId)));
        }

        public Task<List<ZoneDto>> GetAllByFacilityAsync(Guid facilityId, CancellationToken ct = default)
        {
            var zones = _zones.Where(z => z.FacilityId == facilityId).Select(Clone).ToList();
            return Task.FromResult(zones);
        }

        public Task<bool> ExistsInFacilityAsync(Guid zoneId, Guid facilityId, CancellationToken ct = default)
        {
            var result = _zones.Any(z => z.Id == zoneId && z.FacilityId == facilityId);
            return Task.FromResult(result);
        }

        public Task<List<Guid>> GetAncestorIdsAsync(Guid zoneId, CancellationToken ct = default)
        {
            if (_ancestorChains.TryGetValue(zoneId, out var chain))
                return Task.FromResult(new List<Guid>(chain));

            return Task.FromResult(new List<Guid>());
        }

        private static ZoneDto Clone(ZoneDto source)
        {
            return new ZoneDto
            {
                Id = source.Id,
                FacilityId = source.FacilityId,
                Name = source.Name,
                Type = source.Type,
                ParentZoneId = source.ParentZoneId,
                Children = source.Children.Select(Clone).ToList()
            };
        }
    }
}
