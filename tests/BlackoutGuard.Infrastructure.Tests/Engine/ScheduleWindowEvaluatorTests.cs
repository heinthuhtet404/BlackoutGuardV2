using BlackoutGuard.Infrastructure.Engine;

namespace BlackoutGuard.Infrastructure.Tests.Engine;

public class ScheduleWindowEvaluatorTests
{
    // 2026-08-17 is a Monday.
    private static readonly DateTime Monday = new(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(9, 0)]   // exactly at start — inside
    [InlineData(9, 1)]   // just inside
    [InlineData(16, 59)] // just inside end
    [InlineData(17, 0)]  // exactly at end — inside
    public void SameDayWindow_Boundary_Inside(int hour, int minute)
    {
        var now = Monday.AddHours(hour).AddMinutes(minute);

        var result = ScheduleWindowEvaluator.IsInWindow(
            new TimeOnly(9, 0), new TimeOnly(17, 0), new short[] { 1 }, now);

        Assert.True(result);
    }

    [Theory]
    [InlineData(8, 59)]  // one minute before start
    [InlineData(17, 1)]  // one minute after end
    public void SameDayWindow_Boundary_Outside(int hour, int minute)
    {
        var now = Monday.AddHours(hour).AddMinutes(minute);

        var result = ScheduleWindowEvaluator.IsInWindow(
            new TimeOnly(9, 0), new TimeOnly(17, 0), new short[] { 1 }, now);

        Assert.False(result);
    }

    [Fact]
    public void SameDayWindow_WrongDay_IsOutside()
    {
        var tuesday = Monday.AddDays(1);

        var result = ScheduleWindowEvaluator.IsInWindow(
            new TimeOnly(9, 0), new TimeOnly(17, 0), new short[] { 1 }, tuesday);

        Assert.False(result);
    }

    [Fact]
    public void OvernightWrap_Boundary_Inside()
    {
        // 18:00 -> 06:00 (Monday night into Tuesday morning)
        var start = new TimeOnly(18, 0);
        var end = new TimeOnly(6, 0);
        short[] monday = { 1 };

        var atStart = Monday.AddHours(18);
        var lateNight = Monday.AddHours(23).AddMinutes(59);
        var earlyMorning = Monday.AddDays(1).AddHours(6);

        Assert.True(ScheduleWindowEvaluator.IsInWindow(start, end, monday, atStart));
        Assert.True(ScheduleWindowEvaluator.IsInWindow(start, end, monday, lateNight));
        Assert.True(ScheduleWindowEvaluator.IsInWindow(start, end, monday, earlyMorning));
    }

    [Fact]
    public void OvernightWrap_Boundary_Outside()
    {
        var start = new TimeOnly(18, 0);
        var end = new TimeOnly(6, 0);
        short[] monday = { 1 };

        var beforeStart = Monday.AddHours(17).AddMinutes(59);
        var afterEnd = Monday.AddDays(1).AddHours(6).AddMinutes(1);

        Assert.False(ScheduleWindowEvaluator.IsInWindow(start, end, monday, beforeStart));
        Assert.False(ScheduleWindowEvaluator.IsInWindow(start, end, monday, afterEnd));
    }

    [Fact]
    public void OvernightWrap_TailBelongsToPreviousDay()
    {
        // Sunday schedule 18:00 -> 06:00. Monday 05:00 should NOT be in
        // window for a Sunday-only schedule... wait — Sunday's window runs
        // Sunday 18:00 through Monday 06:00, so Monday 05:00 IS the tail of
        // Sunday's schedule.
        var start = new TimeOnly(18, 0);
        var end = new TimeOnly(6, 0);
        short[] sunday = { 7 };

        var mondayMorning = Monday.AddHours(5);
        var sundayEvening = Monday.AddDays(-1).AddHours(19);

        Assert.True(ScheduleWindowEvaluator.IsInWindow(start, end, sunday, mondayMorning));
        Assert.True(ScheduleWindowEvaluator.IsInWindow(start, end, sunday, sundayEvening));
    }
}
