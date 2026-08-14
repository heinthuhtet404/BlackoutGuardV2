using System.Collections.Concurrent;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.BusinessRules;

public enum HysteresisAction
{
    None,
    Shed,
    Restore,
    Locked
}

internal enum HysteresisPhase
{
    Normal,
    ShedPending,
    Shed,
    RestorePending
}

public sealed class HysteresisManager
{
    private readonly TimeSpan _debounce;
    private readonly double _restoreGap;

    private readonly ConcurrentDictionary<Guid, HysteresisPhase> _phases = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _phaseSince = new();

    public HysteresisManager(TimeSpan? debounce = null, double restoreGap = 0.5)
    {
        _debounce = debounce ?? TimeSpan.FromMilliseconds(500);
        _restoreGap = restoreGap;
    }

    public HysteresisAction Evaluate(
        Load load,
        Rule rule,
        LoadCooldownInfo cooldownInfo,
        GridState telemetry,
        DateTime nowUtc)
    {
        // LOCKED STATE — checked BEFORE threshold logic; short-circuit entirely.
        if (cooldownInfo.CooldownUntil is { } until && nowUtc < until)
        {
            return HysteresisAction.Locked;
        }

        var phase = _phases.GetOrAdd(load.Id, HysteresisPhase.Normal);

        var shedTriggered = rule.ParameterKey == "FREQ_HIGH"
            ? telemetry.Frequency > rule.MaxValue
            : telemetry.Frequency < rule.MinValue;

        var restoreOk = rule.ParameterKey == "FREQ_HIGH"
            ? telemetry.Frequency <= rule.MaxValue - _restoreGap
            : telemetry.Frequency >= rule.MinValue + _restoreGap;

        switch (phase)
        {
            case HysteresisPhase.Normal:
                if (shedTriggered)
                {
                    Transition(load.Id, HysteresisPhase.ShedPending, nowUtc);
                }
                return HysteresisAction.None;

            case HysteresisPhase.ShedPending:
                if (!shedTriggered)
                {
                    Transition(load.Id, HysteresisPhase.Normal, nowUtc);
                    return HysteresisAction.None;
                }
                if (ElapsedSince(load.Id, nowUtc) >= _debounce)
                {
                    Transition(load.Id, HysteresisPhase.Shed, nowUtc);
                    return HysteresisAction.Shed;
                }
                return HysteresisAction.None;

            case HysteresisPhase.Shed:
                if (restoreOk)
                {
                    Transition(load.Id, HysteresisPhase.RestorePending, nowUtc);
                }
                return HysteresisAction.None;

            case HysteresisPhase.RestorePending:
                if (!restoreOk)
                {
                    Transition(load.Id, HysteresisPhase.Shed, nowUtc);
                    return HysteresisAction.None;
                }
                if (ElapsedSince(load.Id, nowUtc) >= _debounce)
                {
                    Transition(load.Id, HysteresisPhase.Normal, nowUtc);
                    return HysteresisAction.Restore;
                }
                return HysteresisAction.None;

            default:
                return HysteresisAction.None;
        }
    }

    public static bool IsLocked(LoadCooldownInfo cooldownInfo, DateTime nowUtc) =>
        cooldownInfo.CooldownUntil is { } until && nowUtc < until;

    private void Transition(Guid loadId, HysteresisPhase phase, DateTime nowUtc)
    {
        _phases[loadId] = phase;
        _phaseSince[loadId] = nowUtc;
    }

    private TimeSpan ElapsedSince(Guid loadId, DateTime nowUtc) =>
        nowUtc - _phaseSince.GetOrAdd(loadId, nowUtc);
}
