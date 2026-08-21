using BlackoutGuard.Application;
using BlackoutGuard.Application.DTOs;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Application.UseCases.Loads;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using BlackoutGuard.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace BlackoutGuard.Infrastructure.Tests.UseCases.Loads;

public class CreateLoadConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("blackoutguard_v2")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string _connectionString = string.Empty;
    private Guid _facilityId;
    private Guid _tenantId;
    private Guid _zoneId;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        await using var rlsConnection = new NpgsqlConnection(_connectionString);
        await rlsConnection.OpenAsync();
        await RlsScriptRunner.ApplyAsync(rlsConnection);

        _tenantId = Guid.NewGuid();
        _facilityId = Guid.NewGuid();
        _zoneId = Guid.NewGuid();

        await using var seedContext = CreateDbContext();
        seedContext.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant" });
        seedContext.Facilities.Add(new Facility
        {
            Id = _facilityId,
            TenantId = _tenantId,
            Name = "Test Facility",
            GeneratorCapacityKw = 500
        });
        seedContext.Zones.Add(new Zone
        {
            Id = _zoneId,
            FacilityId = _facilityId,
            Name = "Test Zone",
            Type = "building"
        });
        await seedContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task TwoConcurrentCreates_WithSameRelayAddress_OnlyOneShouldSucceed()
    {
        var results = new List<Result<Guid>>();
        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var useCase = BuildUseCase(db);
            barrier.SignalAndWait();
            var result = await useCase.ExecuteAsync(new CreateLoadRequest
            {
                FacilityId = _facilityId,
                ZoneId = _zoneId,
                Name = "Concurrent Load A",
                RelayAddress = 42,
                PowerRatingKw = 50,
                Priority = "P2"
            });
            lock (results) results.Add(result);
        });

        var task2 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var useCase = BuildUseCase(db);
            barrier.SignalAndWait();
            var result = await useCase.ExecuteAsync(new CreateLoadRequest
            {
                FacilityId = _facilityId,
                ZoneId = _zoneId,
                Name = "Concurrent Load B",
                RelayAddress = 42,
                PowerRatingKw = 50,
                Priority = "P2"
            });
            lock (results) results.Add(result);
        });

        await Task.WhenAll(task1, task2);

        Assert.Equal(2, results.Count);
        var successes = results.Count(r => r.IsSuccess);
        var failures = results.Count(r => !r.IsSuccess);

        Assert.Equal(1, successes);
        Assert.Equal(1, failures);

        var failure = results.Single(r => !r.IsSuccess);
        Assert.Contains("42", failure.ErrorMessage);
        Assert.Contains("assigned", failure.ErrorMessage);
    }

    private BlackoutGuardDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BlackoutGuardDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new BlackoutGuardDbContext(options);
    }

    private static CreateLoadUseCase BuildUseCase(BlackoutGuardDbContext db)
    {
        var loadRepo = new LoadRepository(db);
        var facilityRepo = new FacilityRepository(db);
        var auditRepo = new DecisionAuditLogRepository(db);
        var txFactory = new DbTransactionFactory(db);
        var executionStrategy = new ExecutionStrategy(db);  // ✅ Add this

        return new CreateLoadUseCase(loadRepo, facilityRepo, auditRepo, txFactory, executionStrategy);
    }
}