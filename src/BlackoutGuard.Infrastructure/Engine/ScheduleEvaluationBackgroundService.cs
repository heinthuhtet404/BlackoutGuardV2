using System.Data.Common;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlackoutGuard.Infrastructure.Engine;

public sealed class ScheduleEvaluationBackgroundService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PendingConfigChangeQueue _configQueue;
    private readonly ISystemTimeProvider _timeProvider;
    private readonly ILogger<ScheduleEvaluationBackgroundService> _logger;

    public ScheduleEvaluationBackgroundService(
        IServiceScopeFactory scopeFactory,
        PendingConfigChangeQueue configQueue,
        ISystemTimeProvider timeProvider,
        ILogger<ScheduleEvaluationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configQueue = configQueue;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await EvaluateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schedule evaluation failed");
            }
        }
    }

    public async Task EvaluateAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlackoutGuardDbContext>();

        var now = _timeProvider.UtcNow;

        var facilityIds = await db.Facilities
            .Select(f => f.Id)
            .ToListAsync(ct);

        foreach (var facilityId in facilityIds)
        {
            await EvaluateFacilityAsync(db, facilityId, now, ct);
        }
    }

    private async Task EvaluateFacilityAsync(
        BlackoutGuardDbContext db,
        Guid facilityId,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, ct);
        await SetFacilitySessionAsync(connection, facilityId, ct);

        var schedules = await db.TimeSchedules
            .Where(s => s.FacilityId == facilityId && s.IsActive)
            .ToListAsync(ct);

        foreach (var schedule in schedules)
        {
            if (!ScheduleWindowEvaluator.IsInWindow(
                    schedule.StartTime, schedule.EndTime, schedule.DaysOfWeek, nowUtc))
            {
                continue;
            }

            var load = await db.Loads
                .FirstOrDefaultAsync(l => l.Id == schedule.LoadId && l.FacilityId == facilityId, ct);

            if (load is null)
                continue;

            if (load.Priority == schedule.TargetPriority)
                continue;

            var updatedLoad = new Domain.Entities.Load(
                load.Id,
                load.FacilityId,
                load.ZoneId,
                load.Name,
                load.RelayAddress,
                load.PowerRatingKw,
                schedule.TargetPriority,
                load.PriorityMode,
                load.IsActive,
                load.IsSheddable);

            _configQueue.Enqueue(new LoadChanged(facilityId, nowUtc, updatedLoad));
            _logger.LogInformation(
                "Schedule '{ScheduleName}' in window: load '{LoadName}' priority -> {Priority}",
                schedule.Name, load.Name, schedule.TargetPriority);
        }
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }
    }

    private static async Task SetFacilitySessionAsync(
        DbConnection connection,
        Guid facilityId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SET app.current_facility_id = '{facilityId}'";
        await command.ExecuteNonQueryAsync(ct);
    }
}
