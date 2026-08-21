using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;
using BlackoutGuard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlackoutGuard.Infrastructure.Engine;

public sealed class ScheduleEvaluationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly ILogger<ScheduleEvaluationBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly PendingConfigChangeQueue _configQueue;
    private readonly ITimeProvider _timeProvider;

    public ScheduleEvaluationBackgroundService(
        ILogger<ScheduleEvaluationBackgroundService> logger,
        IServiceProvider serviceProvider,
        PendingConfigChangeQueue configQueue)
        : this(logger, serviceProvider, configQueue, new SystemTimeProvider())
    {
    }

    internal ScheduleEvaluationBackgroundService(
        ILogger<ScheduleEvaluationBackgroundService> logger,
        IServiceProvider serviceProvider,
        PendingConfigChangeQueue configQueue,
        ITimeProvider timeProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configQueue = configQueue ?? throw new ArgumentNullException(nameof(configQueue));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Schedule Evaluation Background Service is starting.");

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await EvaluateSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during schedule evaluation");
            }
        }

        _logger.LogInformation("Schedule Evaluation Background Service is stopping.");
    }

    public async Task EvaluateSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BlackoutGuardDbContext>();

        var schedules = await dbContext.TimeSchedules
            .Where(s => s.IsActive)
            .Include(s => s.Load)
            .Include(s => s.Facility)
            .ToListAsync(cancellationToken);

        if (schedules.Count == 0)
            return;

        var nowUtc = _timeProvider.UtcNow;
        var changesEnqueued = 0;
        var facilityTimezones = new Dictionary<Guid, TimeZoneInfo>();

        foreach (var schedule in schedules)
        {
            try
            {
                if (schedule.Load == null)
                {
                    _logger.LogWarning(
                        "Schedule {ScheduleId} references a Load that no longer exists. Skipping.",
                        schedule.Id);
                    continue;
                }

                if (!facilityTimezones.TryGetValue(schedule.FacilityId, out var timeZone))
                {
                    var timezoneId = schedule.Facility?.TimezoneId ?? "UTC";
                    try
                    {
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        _logger.LogWarning(
                            "Timezone '{TimezoneId}' not found for Facility {FacilityId}. Falling back to UTC.",
                            timezoneId,
                            schedule.FacilityId);
                        timeZone = TimeZoneInfo.Utc;
                    }
                    facilityTimezones[schedule.FacilityId] = timeZone;
                }

                var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
                var currentTime = TimeOnly.FromDateTime(localNow);

                bool isInWindow = IsTimeInWindow(currentTime, schedule.StartTime, schedule.EndTime);

                if (!isInWindow)
                    continue;

                if (schedule.TargetPriority == schedule.Load.Priority)
                    continue;

                var updatedLoad = new BlackoutGuard.Domain.Entities.Load(
                    schedule.Load.Id,
                    schedule.Load.FacilityId,
                    schedule.Load.ZoneId,
                    schedule.Load.Name,
                    schedule.Load.RelayAddress,
                    schedule.Load.PowerRatingKw,
                    schedule.TargetPriority,
                    schedule.Load.PriorityMode,
                    schedule.Load.IsActive,
                    schedule.Load.IsSheddable
                );

                var change = new LoadChanged(
                    schedule.FacilityId,
                    DateTime.UtcNow,
                    updatedLoad);

                _configQueue.Enqueue(change);
                changesEnqueued++;

                _logger.LogDebug(
                    "Enqueued priority change for Load {LoadId}: {OldPriority} -> {NewPriority} " +
                    "due to schedule {ScheduleId} at local time {LocalTime}",
                    schedule.LoadId,
                    schedule.Load.Priority,
                    schedule.TargetPriority,
                    schedule.Id,
                    localNow.ToString("HH:mm:ss"));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error evaluating schedule {ScheduleId} for load {LoadId}",
                    schedule.Id,
                    schedule.LoadId);
            }
        }

        if (changesEnqueued > 0)
        {
            _logger.LogInformation(
                "Enqueued {Count} priority changes from schedule evaluation",
                changesEnqueued);
        }
    }

    private static bool IsTimeInWindow(TimeOnly current, TimeOnly start, TimeOnly end)
    {
        if (start <= end)
        {
            return current >= start && current <= end;
        }
        else
        {
            return current >= start || current <= end;
        }
    }
}