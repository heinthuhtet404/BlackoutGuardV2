using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.Services;

namespace BlackoutGuard.Domain.BusinessRules;

public class AlarmRuleEngine : IAlarmGenerator
{
    private readonly Dictionary<string, DateTime?> _lastAlarmTime = new();

    public IEnumerable<Alarm> GenerateAlarms(GridState gridState)
    {
        var alarms = new List<Alarm>();

        // Frequency alarms (using Frequency property)
        if (gridState.Frequency < 48.5)
        {
            alarms.Add(Alarm.CreateCritical(
                "FREQ_CRITICAL_LOW",
                $"Grid frequency critically low: {gridState.Frequency:F2} Hz. " +
                "Emergency load shedding may be activated."));
        }
        else if (gridState.Frequency < 49.0)
        {
            alarms.Add(Alarm.CreateWarning(
                "FREQ_WARNING",
                $"Grid frequency warning: {gridState.Frequency:F2} Hz. " +
                "System approaching critical condition."));
        }

        // Voltage alarms
        if (gridState.Voltage < 207)
        {
            alarms.Add(Alarm.CreateWarning(
                "VOLTAGE_LOW",
                $"Grid voltage low: {gridState.Voltage:F1} V. " +
                "System may be unstable."));
        }
        else if (gridState.Voltage > 253)
        {
            alarms.Add(Alarm.CreateWarning(
                "VOLTAGE_HIGH",
                $"Grid voltage high: {gridState.Voltage:F1} V. " +
                "System may be unstable."));
        }

        // Generator alarm
        if (!gridState.GeneratorOn)
        {
            alarms.Add(Alarm.CreateCritical(
                "GENERATOR_OFF",
                "Generator is OFF. Grid may be running on backup or unstable."));
        }

        // Deduplicate alarms
        return DeduplicateAlarms(alarms);
    }

    private List<Alarm> DeduplicateAlarms(List<Alarm> alarms)
    {
        var deduplicated = new List<Alarm>();
        var now = DateTime.UtcNow;

        foreach (var alarm in alarms)
        {
            if (_lastAlarmTime.TryGetValue(alarm.AlarmCode, out var lastTime))
            {
                var timeSinceLastAlarm = (now - lastTime?.ToUniversalTime())?.TotalMilliseconds ?? 0;
                if (timeSinceLastAlarm < 5000) // 5 seconds suppress
                    continue;
            }

            _lastAlarmTime[alarm.AlarmCode] = now;
            deduplicated.Add(alarm);
        }

        return deduplicated;
    }
}