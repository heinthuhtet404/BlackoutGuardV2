namespace BlackoutGuard.Infrastructure.Engine;

public static class ScheduleWindowEvaluator
{
    private const int Monday = 1;
    private const int Sunday = 7;

    public static bool IsInWindow(
        TimeOnly start,
        TimeOnly end,
        IEnumerable<short> daysOfWeek,
        DateTime utcNow)
    {
        var days = new HashSet<short>(daysOfWeek);
        if (days.Count == 0)
            return false;

        var nowTime = TimeOnly.FromDateTime(utcNow);
        var today = (short)DayIndex(utcNow.DayOfWeek);
        var yesterday = (short)(today == Monday ? Sunday : today - 1);

        if (start <= end)
        {
            // Same-day window: day must match and now must be within [start, end].
            return days.Contains(today) && nowTime >= start && nowTime <= end;
        }

        // Overnight wrap (e.g. 18:00 -> 06:00):
        //   today's portion:   day matches today   AND now >= start (until midnight)
        //   yesterday's tail:  day matches yesterday AND now <= end (from midnight)
        var todayCase = days.Contains(today) && nowTime >= start;
        var yesterdayCase = days.Contains(yesterday) && nowTime <= end;
        return todayCase || yesterdayCase;
    }

    private static int DayIndex(DayOfWeek dayOfWeek) =>
        dayOfWeek == DayOfWeek.Sunday ? Sunday : (int)dayOfWeek;
}
