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

    private string _adminConnectionString = string.Empty;
    private string _appUserConnectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _adminConnectionString = _container.GetConnectionString();

        // 1. Migration နှင့် RLS Script ကို Admin (postgres) အကောင့်ဖြင့် Run ပါ
        await using (var context = CreateDbContext(_adminConnectionString))
        {
            await context.Database.MigrateAsync();
        }

        await using (var rlsConnection = new NpgsqlConnection(_adminConnectionString))
        {
            await rlsConnection.OpenAsync();
            await RlsScriptRunner.ApplyAsync(rlsConnection);

            // 2. Superuser မဟုတ်သော App Role သီးသန့်ဆောက်ပြီး Permissions ပေးပါ
            await using var cmd = rlsConnection.CreateCommand();
            cmd.CommandText = @"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_user') THEN
                        CREATE ROLE app_user WITH LOGIN PASSWORD 'app_password';
                    END IF;
                END $$;
                GRANT CONNECT ON DATABASE blackoutguard_v2 TO app_user;
                GRANT USAGE ON SCHEMA public TO app_user;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
            ";
            await cmd.ExecuteNonQueryAsync();
        }

        // Connection string တွင် app_user ကို ပြောင်းသုံးပါ
        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Username = "app_user",
            Password = "app_password"
        };
        _appUserConnectionString = builder.ConnectionString;
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

        // Data ထည့်ခြင်းကို Admin Context ဖြင့် ဆောင်ရွက်ပါ
        await using (var adminContext = CreateDbContext(_adminConnectionString))
        {
            await SeedIsolationData(adminContext, facilityAId, facilityBId);
        }

        // Query စစ်ခြင်းကို Non-Superuser (app_user) Context ဖြင့် စစ်ပါ
        await using var userContext = CreateDbContext(_appUserConnectionString);
        await userContext.Database.OpenConnectionAsync();

        // Session Variable သတ်မှတ်ပါ
        await userContext.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.current_facility_id', {facilityAId.ToString()}, false);");

        var loads = await userContext.Loads.AsNoTracking().ToListAsync();

        Assert.NotEmpty(loads);
        Assert.All(loads, l => Assert.Equal(facilityAId, l.FacilityId));
        Assert.DoesNotContain(loads, l => l.FacilityId == facilityBId);
    }

    [Fact]
    public async Task Zones_ShouldBeIsolatedByFacilityId_WhenRlsIsEnabled()
    {
        var facilityAId = Guid.NewGuid();
        var facilityBId = Guid.NewGuid();

        // Data ထည့်ခြင်းကို Admin Context ဖြင့် ဆောင်ရွက်ပါ
        await using (var adminContext = CreateDbContext(_adminConnectionString))
        {
            await SeedIsolationData(adminContext, facilityAId, facilityBId);
        }

        // Query စစ်ခြင်းကို Non-Superuser (app_user) Context ဖြင့် စစ်ပါ
        await using var userContext = CreateDbContext(_appUserConnectionString);
        await userContext.Database.OpenConnectionAsync();

        // Session Variable သတ်မှတ်ပါ
        await userContext.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.current_facility_id', {facilityAId.ToString()}, false);");

        var zones = await userContext.Zones.AsNoTracking().ToListAsync();

        Assert.NotEmpty(zones);
        Assert.All(zones, z => Assert.Equal(facilityAId, z.FacilityId));
        Assert.DoesNotContain(zones, z => z.FacilityId == facilityBId);
    }

    private async Task SeedIsolationData(BlackoutGuardDbContext context, Guid facilityAId, Guid facilityBId)
    {
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

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

    private BlackoutGuardDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BlackoutGuardDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BlackoutGuardDbContext(options);
    }
}