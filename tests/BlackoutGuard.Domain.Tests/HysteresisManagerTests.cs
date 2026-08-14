using BlackoutGuard.Domain.BusinessRules;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.ValueObjects;

namespace BlackoutGuard.Domain.Tests.BusinessRules;

public class HysteresisManagerTests
{
    private static readonly DateTime BaseTime = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Load MakeLoad(string name = "Load", int relay = 1) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        name, relay, 50.0, "P1", "auto", true, true);

    private static Rule MakeFreqLowRule(
        int cooldownSeconds = 30,
        double minValue = 48.0,
        double maxValue = 49.5) => new(
        Guid.NewGuid(), Guid.NewGuid(), "FREQ_LOW",
        minValue, maxValue, cooldownSeconds, true);

    private static GridState MakeTelemetry(double frequency) => new()
    {
        Frequency = frequency,
        Voltage = 230.0,
        TotalLoad = 100.0,
        GeneratorOn = true,
        IsBreakerTripped = false,
        TimestampUtc = BaseTime
    };

    [Fact]
    public void LoadInsideCooldown_IsNeverShed_EvenWhenFrequencyCrossesCriticalThreshold()
    {
        var manager = new HysteresisManager(debounce: TimeSpan.Zero);
        var load = MakeLoad();
        var rule = MakeFreqLowRule(cooldownSeconds: 60);
        var cooldown = new LoadCooldownInfo(
            LastShedAt: BaseTime,
            LastRestoredAt: null,
            CooldownUntil: BaseTime.AddSeconds(30));
        var telemetry = MakeTelemetry(45.0); // way below 48.0 threshold

        var action = manager.Evaluate(load, rule, cooldown, telemetry, BaseTime);

        Assert.Equal(HysteresisAction.Locked, action);

        // Still locked as time advances within the window.
        var later = BaseTime.AddSeconds(20);
        action = manager.Evaluate(load, rule, cooldown, telemetry, later);
        Assert.Equal(HysteresisAction.Locked, action);
    }

    [Fact]
    public void LoadWhoseCooldownJustExpired_IsEligibleAgain_OnTheVeryNextTick()
    {
        var manager = new HysteresisManager(debounce: TimeSpan.Zero);
        var load = MakeLoad();
        var rule = MakeFreqLowRule(cooldownSeconds: 30);
        var cooldown = new LoadCooldownInfo(
            LastShedAt: BaseTime.AddSeconds(-30),
            LastRestoredAt: null,
            CooldownUntil: BaseTime);
        var telemetry = MakeTelemetry(45.0);

        // At exactly CooldownUntil the load is no longer locked.
        Assert.False(HysteresisManager.IsLocked(cooldown, BaseTime));

        // First tick enters ShedPending (debounce = 0), second tick sheds.
        var first = manager.Evaluate(load, rule, cooldown, telemetry, BaseTime);
        Assert.NotEqual(HysteresisAction.Locked, first);

        var second = manager.Evaluate(
            load, rule, cooldown, telemetry, BaseTime.AddMilliseconds(100));
        Assert.Equal(HysteresisAction.Shed, second);
    }

    [Fact]
    public void TwoLoadsWithDifferentCooldowns_AreTrackedIndependently()
    {
        var manager = new HysteresisManager(debounce: TimeSpan.Zero);
        var telemetry = MakeTelemetry(45.0);

        var loadA = MakeLoad("Load A", relay: 1);
        var ruleA = MakeFreqLowRule(cooldownSeconds: 10);
        var cooldownA = new LoadCooldownInfo(
            LastShedAt: BaseTime,
            LastRestoredAt: null,
            CooldownUntil: BaseTime.AddSeconds(10));

        var loadB = MakeLoad("Load B", relay: 2);
        var ruleB = MakeFreqLowRule(cooldownSeconds: 60);
        var cooldownB = new LoadCooldownInfo(
            LastShedAt: BaseTime,
            LastRestoredAt: null,
            CooldownUntil: BaseTime.AddSeconds(60));

        // At t+5s: A still locked (10s), B still locked (60s).
        var t = BaseTime.AddSeconds(5);
        Assert.Equal(
            HysteresisAction.Locked,
            manager.Evaluate(loadA, ruleA, cooldownA, telemetry, t));
        Assert.Equal(
            HysteresisAction.Locked,
            manager.Evaluate(loadB, ruleB, cooldownB, telemetry, t));

        // At t+11s: A's cooldown expired → eligible (ShedPending), B still locked.
        t = BaseTime.AddSeconds(11);
        var actionA = manager.Evaluate(loadA, ruleA, cooldownA, telemetry, t);
        Assert.NotEqual(HysteresisAction.Locked, actionA);

        var actionB = manager.Evaluate(loadB, ruleB, cooldownB, telemetry, t);
        Assert.Equal(HysteresisAction.Locked, actionB);

        // At t+61s: B's cooldown expired → eligible too.
        t = BaseTime.AddSeconds(61);
        var actionB2 = manager.Evaluate(loadB, ruleB, cooldownB, telemetry, t);
        Assert.NotEqual(HysteresisAction.Locked, actionB2);
    }

    [Fact]
    public void Shed_RequiresDebouncePeriod_BeforeActivating()
    {
        var manager = new HysteresisManager(debounce: TimeSpan.FromSeconds(1));
        var load = MakeLoad();
        var rule = MakeFreqLowRule();
        var cooldown = new LoadCooldownInfo(null, null, null);
        var telemetry = MakeTelemetry(45.0);

        Assert.Equal(
            HysteresisAction.None,
            manager.Evaluate(load, rule, cooldown, telemetry, BaseTime));
        Assert.Equal(
            HysteresisAction.None,
            manager.Evaluate(load, rule, cooldown, telemetry, BaseTime.AddMilliseconds(500)));
        Assert.Equal(
            HysteresisAction.Shed,
            manager.Evaluate(load, rule, cooldown, telemetry, BaseTime.AddSeconds(1)));
    }

    [Fact]
    public void Restore_RequiresFrequencyAboveThresholdPlusGap_AndDebounce()
    {
        var manager = new HysteresisManager(
            debounce: TimeSpan.FromSeconds(1),
            restoreGap: 0.5);
        var load = MakeLoad();
        var rule = MakeFreqLowRule(minValue: 48.0, maxValue: 49.5);
        var cooldown = new LoadCooldownInfo(null, null, null);

        // Shed at 45 Hz (first call enters ShedPending, debounce elapses).
        Assert.Equal(
            HysteresisAction.None,
            manager.Evaluate(
                load, rule, cooldown, MakeTelemetry(45.0), BaseTime));
        Assert.Equal(
            HysteresisAction.Shed,
            manager.Evaluate(
                load, rule, cooldown, MakeTelemetry(45.0), BaseTime.AddSeconds(1)));

        // 48.2 Hz is above min (48.0) but below min+gap (48.5) → stay shed.
        Assert.Equal(
            HysteresisAction.None,
            manager.Evaluate(
                load, rule, cooldown, MakeTelemetry(48.2), BaseTime.AddSeconds(1)));

        // 48.6 Hz is above the gap → restore pending, needs debounce.
        Assert.Equal(
            HysteresisAction.None,
            manager.Evaluate(
                load, rule, cooldown, MakeTelemetry(48.6), BaseTime.AddSeconds(2)));

        // Debounce elapsed → restore.
        Assert.Equal(
            HysteresisAction.Restore,
            manager.Evaluate(
                load, rule, cooldown, MakeTelemetry(48.6), BaseTime.AddSeconds(3)));
    }

    [Fact]
    public void NormalFrequency_ProducesNoAction()
    {
        var manager = new HysteresisManager();
        var load = MakeLoad();
        var rule = MakeFreqLowRule();
        var cooldown = new LoadCooldownInfo(null, null, null);

        var action = manager.Evaluate(
            load, rule, cooldown, MakeTelemetry(50.0), BaseTime);

        Assert.Equal(HysteresisAction.None, action);
    }
}
