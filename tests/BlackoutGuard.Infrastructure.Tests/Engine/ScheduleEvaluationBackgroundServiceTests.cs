using BlackoutGuard.Infrastructure.Engine;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace BlackoutGuard.Infrastructure.Tests.Engine;

public class ScheduleEvaluationBackgroundServiceTests : IAsyncLifetime
{
    private const string TestDatabase = "blackoutguard_v2_schedule_test";

    private ServiceProvider _services = null!;
    private BlackoutGuardDbContext _db = null!;
    private PendingConfigChangeQueue _queue = null!;
    private FakeTimeProvider _timeProvider = null!;
    private ScheduleEvaluationBackgroundService _service = null!;

    private Guid _facilityId;
    private Guid _zoneId;
    private Guid _loadId;

    public async Task InitializeAsync()
    {
        await EnsureTestDatabaseAsync();

        var connectionString =
            $"Host=localhost;Database={TestDatabase};Username=postgres;Password=postgres";

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<BlackoutGuardDbContext>(options =>
            options.UseNpgsql(connectionString));
        serviceCollection.AddSingleton<PendingConfigChangeQueue>();
        serviceCollection.AddLogging();

        _services = serviceCollection.BuildServiceProvider();
        _db = _services.GetRequiredService<BlackoutGuardDbContext>();
        await _db.Database.MigrateAsync();

        _queue = new PendingConfigChangeQueue();
        _timeProvider = new FakeTimeProvider();

        _service = new ScheduleEvaluationBackgroundService(
            _services.GetRequiredService<IServiceScopeFactory>(),
            _queue,
            _timeProvider,
            NullLogger<ScheduleEvaluationBackgroundService>.Instance);

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await DropTestDatabaseAsync();
    }

    [Fact]
    public async Task ScheduleInWindow_WithDifferentPriority_EnqueuesLoadChanged()
    {
        var now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc); // Monday noon
        _timeProvider.UtcNow = now;

        _db.TimeSchedules.Add(new TimeSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            Name = "Afternoon Shed",
            LoadId = _loadId,
            TargetPriority = "P1",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            DaysOfWeek = new short[] { 1 },
            IsActive = true
        });
        await _db.SaveChangesAsync();

        await _service.EvaluateAsync();

        var changes = _queue.DrainAll();
        var change = Assert.Single(changes);
        var loadChanged = Assert.IsType<LoadChanged>(change);
        Assert.Equal(_facilityId, loadChanged.FacilityId);
        Assert.Equal(_loadId, loadChanged.UpdatedLoad.Id);
        Assert.Equal("P1", loadChanged.UpdatedLoad.Priority);
    }

    [Fact]
    public async Task ScheduleOutOfWindow_EnqueuesNothing()
    {
        var now = new DateTime(2026, 8, 17, 8, 30, 0, DateTimeKind.Utc); // Monday before 9am
        _timeProvider.UtcNow = now;

        _db.TimeSchedules.Add(new TimeSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            Name = "Afternoon Shed",
            LoadId = _loadId,
            TargetPriority = "P1",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            DaysOfWeek = new short[] { 1 },
            IsActive = true
        });
        await _db.SaveChangesAsync();

        await _service.EvaluateAsync();

        Assert.Empty(_queue.DrainAll());
    }

    [Fact]
    public async Task ScheduleInWindow_ButPriorityUnchanged_EnqueuesNothing()
    {
        var now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
        _timeProvider.UtcNow = now;

        _db.TimeSchedules.Add(new TimeSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = _facilityId,
            Name = "No-op Schedule",
            LoadId = _loadId,
            TargetPriority = "P2", // load is already P2
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            DaysOfWeek = new short[] { 1 },
            IsActive = true
        });
        await _db.SaveChangesAsync();

        await _service.EvaluateAsync();

        Assert.Empty(_queue.DrainAll());
    }

    private async Task SeedAsync()
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Schedule Test Tenant" };
        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Schedule Test Facility",
            GeneratorCapacityKW = 500
        };
        var zone = new Zone
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            Name = "Test Zone",
            Type = "building"
        };
        var load = new Load
        {
            Id = Guid.NewGuid(),
            FacilityId = facility.Id,
            ZoneId = zone.Id,
            Name = "Schedulable Load",
            RelayAddress = 1,
            PowerRatingKw = 50,
            Priority = "P2"
        };

        _db.Tenants.Add(tenant);
        _db.Facilities.Add(facility);
        _db.Zones.Add(zone);
        _db.Loads.Add(load);
        await _db.SaveChangesAsync();

        _facilityId = facility.Id;
        _zoneId = zone.Id;
        _loadId = load.Id;
    }

    private sealed class FakeTimeProvider : ISystemTimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private static async Task EnsureTestDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(
            "Host=localhost;Database=postgres;Username=postgres;Password=postgres");
        await connection.OpenAsync();

        await using (var drop = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{TestDatabase}\" WITH (FORCE)", connection))
        {
            await drop.ExecuteNonQueryAsync();
        }

        await using (var create = new NpgsqlCommand(
            $"CREATE DATABASE \"{TestDatabase}\"", connection))
        {
            await create.ExecuteNonQueryAsync();
        }
    }

    private static async Task DropTestDatabaseAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(
                "Host=localhost;Database=postgres;Username=postgres;Password=postgres");
            await connection.OpenAsync();

            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{TestDatabase}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
