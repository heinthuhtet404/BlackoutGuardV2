using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace BlackoutGuard.Infrastructure.Tests.Persistence;

public class TenantIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("blackoutguard_v2")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        await using var rlsConnection = new NpgsqlConnection(_connectionString);
        await rlsConnection.OpenAsync();
        await RlsScriptRunner.ApplyAsync(rlsConnection);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Loads_ShouldBeIsolatedByFacilityId_WhenRlsIsEnabled()
    {
        var facilityAId = Guid.NewGuid();
        var facilityBId = Guid.NewGuid();

        await SeedIsolationData(facilityAId, facilityBId);

        await using var context = CreateDbContext();
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            $"SET app.current_facility_id = '{facilityAId}'", connection);
        await cmd.ExecuteNonQueryAsync();

        var loads = await context.Loads.ToListAsync();

        Assert.NotEmpty(loads);
        Assert.All(loads, l => Assert.Equal(facilityAId, l.FacilityId));
        Assert.DoesNotContain(loads, l => l.FacilityId == facilityBId);
    }

    [Fact]
    public async Task Zones_ShouldBeIsolatedByFacilityId_WhenRlsIsEnabled()
    {
        var facilityAId = Guid.NewGuid();
        var facilityBId = Guid.NewGuid();

        await SeedIsolationData(facilityAId, facilityBId);

        await using var context = CreateDbContext();
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            $"SET app.current_facility_id = '{facilityAId}'", connection);
        await cmd.ExecuteNonQueryAsync();

        var zones = await context.Zones.ToListAsync();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(facilityAId, z.FacilityId));
        Assert.DoesNotContain(zones, z => z.FacilityId == facilityBId);
    }

    private async Task SeedIsolationData(Guid facilityAId, Guid facilityBId)
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        await using var context = CreateDbContext();

        context.Tenants.AddRange(
            new Tenant { Id = tenant1Id, Name = "Tenant One" },
            new Tenant { Id = tenant2Id, Name = "Tenant Two" }
        );

        context.Facilities.AddRange(
            new Facility { Id = facilityAId, TenantId = tenant1Id, Name = "Facility A" },
            new Facility { Id = facilityBId, TenantId = tenant2Id, Name = "Facility B" }
        );

        var zoneA = new Zone
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityAId,
            Name = "Zone A",
            Type = "building"
        };

        var zoneB = new Zone
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityBId,
            Name = "Zone B",
            Type = "building"
        };

        context.Zones.AddRange(zoneA, zoneB);

        context.Loads.AddRange(
            new Load
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityAId,
                ZoneId = zoneA.Id,
                Name = "Load A",
                RelayAddress = 1,
                PowerRatingKw = 10.0,
                Priority = "P1"
            },
            new Load
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityBId,
                ZoneId = zoneB.Id,
                Name = "Load B",
                RelayAddress = 1,
                PowerRatingKw = 20.0,
                Priority = "P1"
            }
        );

        await context.SaveChangesAsync();
    }

    private BlackoutGuardDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BlackoutGuardDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new BlackoutGuardDbContext(options);
    }
}
