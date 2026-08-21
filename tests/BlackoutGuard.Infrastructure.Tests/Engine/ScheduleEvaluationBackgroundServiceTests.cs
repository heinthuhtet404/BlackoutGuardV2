using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackoutGuard.Domain.ValueObjects;
using BlackoutGuard.Infrastructure.Engine;
using BlackoutGuard.Infrastructure.Persistence;
using BlackoutGuard.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlackoutGuard.Infrastructure.Tests.Engine;

public sealed class ScheduleEvaluationBackgroundServiceTests : IDisposable
{
    private readonly Mock<ILogger<ScheduleEvaluationBackgroundService>> _loggerMock;
    private readonly PendingConfigChangeQueue _queue;
    private readonly BlackoutGuardDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly Guid _facilityId;
    private readonly Guid _tenantId;
    private readonly Mock<ITimeProvider> _timeProviderMock;

    public ScheduleEvaluationBackgroundServiceTests()
    {
        _loggerMock = new Mock<ILogger<ScheduleEvaluationBackgroundService>>();
        _queue = new PendingConfigChangeQueue();
        _timeProviderMock = new Mock<ITimeProvider>();
        _tenantId = Guid.NewGuid();
        _facilityId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<BlackoutGuardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new BlackoutGuardDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddSingleton(_loggerMock.Object);
        services.AddSingleton(_queue);
        services.AddSingleton(_timeProviderMock.Object);

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task EvaluateSchedulesAsync_ShouldEnqueueLoadChanged_WhenScheduleIsActiveAndInWindow()
    {
        // Arrange - Fixed time: 10:00 AM (inside 9:00-17:00 window)
        var fixedTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        _timeProviderMock.Setup(tp => tp.UtcNow).Returns(fixedTime);

        var facility = CreateFacility("Test Facility", "Asia/Yangon");
        var load = CreateLoad("Load1", "P2", facility.Id);
        var schedule = CreateTimeSchedule(load.Id, facility.Id, new TimeOnly(9, 0), new TimeOnly(17, 0), "P1", true);

        _dbContext.Facilities.Add(facility);
        _dbContext.Loads.Add(load);
        _dbContext.TimeSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.EvaluateSchedulesAsync(CancellationToken.None);

        // Assert
        var changes = _queue.DrainAll();
        Assert.Single(changes);
        var loadChanged = Assert.IsType<LoadChanged>(changes[0]);
        Assert.Equal(load.Id, loadChanged.UpdatedLoad.Id);
        Assert.Equal("P1", loadChanged.UpdatedLoad.Priority);
        Assert.Equal(_facilityId, loadChanged.FacilityId);
    }

    [Fact]
    public async Task EvaluateSchedulesAsync_ShouldNotEnqueue_WhenScheduleIsInactive()
    {
        // Arrange
        var fixedTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        _timeProviderMock.Setup(tp => tp.UtcNow).Returns(fixedTime);

        var facility = CreateFacility("Test Facility", "Asia/Yangon");
        var load = CreateLoad("Load1", "P2", facility.Id);
        var schedule = CreateTimeSchedule(load.Id, facility.Id, new TimeOnly(9, 0), new TimeOnly(17, 0), "P1", false);

        _dbContext.Facilities.Add(facility);
        _dbContext.Loads.Add(load);
        _dbContext.TimeSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.EvaluateSchedulesAsync(CancellationToken.None);

        // Assert
        var changes = _queue.DrainAll();
        Assert.Empty(changes);
    }

    [Fact]
    public async Task EvaluateSchedulesAsync_ShouldNotEnqueue_WhenPriorityAlreadyMatches()
    {
        // Arrange
        var fixedTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        _timeProviderMock.Setup(tp => tp.UtcNow).Returns(fixedTime);

        var facility = CreateFacility("Test Facility", "Asia/Yangon");
        var load = CreateLoad("Load1", "P1", facility.Id);
        var schedule = CreateTimeSchedule(load.Id, facility.Id, new TimeOnly(9, 0), new TimeOnly(17, 0), "P1", true);

        _dbContext.Facilities.Add(facility);
        _dbContext.Loads.Add(load);
        _dbContext.TimeSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.EvaluateSchedulesAsync(CancellationToken.None);

        // Assert
        var changes = _queue.DrainAll();
        Assert.Empty(changes);
    }

    [Fact]
    public async Task EvaluateSchedulesAsync_ShouldHandleOvernightWrap_WhenTimeIsInside()
    {
        // Arrange - Overnight window: 22:00-06:00, current time: 23:00 (inside)
        var fixedTime = new DateTime(2024, 1, 1, 23, 0, 0, DateTimeKind.Utc);
        _timeProviderMock.Setup(tp => tp.UtcNow).Returns(fixedTime);

        var facility = CreateFacility("Test Facility", "Asia/Yangon");
        var load = CreateLoad("Load1", "P2", facility.Id);
        var schedule = CreateTimeSchedule(load.Id, facility.Id, new TimeOnly(22, 0), new TimeOnly(6, 0), "P1", true);

        _dbContext.Facilities.Add(facility);
        _dbContext.Loads.Add(load);
        _dbContext.TimeSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.EvaluateSchedulesAsync(CancellationToken.None);

        // Assert
        var changes = _queue.DrainAll();
        Assert.Single(changes);
        var loadChanged = Assert.IsType<LoadChanged>(changes[0]);
        Assert.Equal(load.Id, loadChanged.UpdatedLoad.Id);
        Assert.Equal("P1", loadChanged.UpdatedLoad.Priority);
    }

    [Fact]
    public async Task EvaluateSchedulesAsync_ShouldNotEnqueue_WhenOvernightWrapIsOutside()
    {
        // Arrange - Overnight window: 22:00-06:00, current time: 12:00 (outside)
        var fixedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _timeProviderMock.Setup(tp => tp.UtcNow).Returns(fixedTime);

        var facility = CreateFacility("Test Facility", "Asia/Yangon");
        var load = CreateLoad("Load1", "P2", facility.Id);
        var schedule = CreateTimeSchedule(load.Id, facility.Id, new TimeOnly(22, 0), new TimeOnly(6, 0), "P1", true);

        _dbContext.Facilities.Add(facility);
        _dbContext.Loads.Add(load);
        _dbContext.TimeSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.EvaluateSchedulesAsync(CancellationToken.None);

        // Assert
        var changes = _queue.DrainAll();
        Assert.Empty(changes);
    }

    [Fact]
    public void IsTimeInWindow_ShouldReturnTrue_WhenTimeIsInsideNormalWindow()
    {
        var current = new TimeOnly(12, 0);
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        var result = IsTimeInWindow(current, start, end);
        Assert.True(result);
    }

    [Fact]
    public void IsTimeInWindow_ShouldReturnFalse_WhenTimeIsOutsideNormalWindow()
    {
        var current = new TimeOnly(8, 0);
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        var result = IsTimeInWindow(current, start, end);
        Assert.False(result);
    }

    [Fact]
    public void IsTimeInWindow_ShouldReturnTrue_WhenTimeIsAtBoundaryStart()
    {
        var current = new TimeOnly(9, 0);
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        var result = IsTimeInWindow(current, start, end);
        Assert.True(result);
    }

    [Fact]
    public void IsTimeInWindow_ShouldReturnTrue_WhenTimeIsAtBoundaryEnd()
    {
        var current = new TimeOnly(17, 0);
        var start = new TimeOnly(9, 0);
        var end = new TimeOnly(17, 0);

        var result = IsTimeInWindow(current, start, end);
        Assert.True(result);
    }

    [Fact]
    public void IsTimeInWindow_ShouldHandleOvernightWrap_WhenTimeIsInside()
    {
        var current = new TimeOnly(23, 0);
        var start = new TimeOnly(22, 0);
        var end = new TimeOnly(6, 0);

        var result = IsTimeInWindow(current, start, end);
        Assert.True(result);
    }

    [Fact]
    public void IsTimeInWindow_ShouldHandleOvernightWrap_WhenTimeIsInsideEarlyMorning()
    {
        var current = new TimeOnly(1, 0);
        var start = new TimeOnly(22, 0);
        var end = new TimeOnly(6, 0);

        var result = IsTimeInWindow(current, start, end);
        Assert.True(result);
    }

    [Fact]
    public void IsTimeInWindow_ShouldHandleOvernightWrap_WhenTimeIsOutside()
    {
        var current = new TimeOnly(12, 0);
        var start = new TimeOnly(22, 0);
        var end = new TimeOnly(6, 0);

        var result = IsTimeInWindow(current, start, end);
        Assert.False(result);
    }

    [Fact]
    public async Task EvaluateSchedulesAsync_ShouldUseSharedQueue_NotCreateNewPath()
    {
        // Arrange
        var fixedTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        _timeProviderMock.Setup(tp => tp.UtcNow).Returns(fixedTime);

        var facility = CreateFacility("Test Facility", "UTC");
        var load = CreateLoad("Load1", "P2", facility.Id);
        var schedule = CreateTimeSchedule(load.Id, facility.Id, new TimeOnly(9, 0), new TimeOnly(17, 0), "P1", true);

        _dbContext.Facilities.Add(facility);
        _dbContext.Loads.Add(load);
        _dbContext.TimeSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Act
        await service.EvaluateSchedulesAsync(CancellationToken.None);

        // Assert
        var changes = _queue.DrainAll();
        Assert.Single(changes);
        Assert.IsType<LoadChanged>(changes[0]);
    }

    private Facility CreateFacility(string name, string timezoneId)
    {
        return new Facility
        {
            Id = _facilityId,
            TenantId = _tenantId,
            Name = name,
            GeneratorCapacityKw = 100.0,
            TimezoneId = timezoneId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private Load CreateLoad(string name, string priority, Guid facilityId)
    {
        return new Load
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ZoneId = Guid.NewGuid(),
            Name = name,
            RelayAddress = 1,
            PowerRatingKw = 10.0,
            Priority = priority,
            PriorityMode = "auto",
            IsActive = true,
            IsSheddable = true
        };
    }

    private TimeSchedule CreateTimeSchedule(
        Guid loadId,
        Guid facilityId,
        TimeOnly startTime,
        TimeOnly endTime,
        string targetPriority,
        bool isActive = true)
    {
        return new TimeSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            LoadId = loadId,
            Name = $"Schedule for Load {loadId}",
            StartTime = startTime,
            EndTime = endTime,
            TargetPriority = targetPriority,
            DaysOfWeek = new short[] { 1, 2, 3, 4, 5, 6, 7 },
            IsActive = isActive
        };
    }

    private ScheduleEvaluationBackgroundService CreateService()
    {
        return new ScheduleEvaluationBackgroundService(
            _loggerMock.Object,
            _serviceProvider,
            _queue);
    }

    private static bool IsTimeInWindow(TimeOnly current, TimeOnly start, TimeOnly end)
    {
        if (start <= end)
            return current >= start && current <= end;
        else
            return current >= start || current <= end;
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}