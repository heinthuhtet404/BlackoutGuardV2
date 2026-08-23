using System.Diagnostics;
using BlackoutGuard.Application.Services;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.Services;
using BlackoutGuard.Domain.ValueObjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlackoutGuard.Infrastructure.Engine;

public sealed class EngineBackgroundService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

    private readonly IDataSource _dataSource;
    private readonly IDecisionStrategy _decisionStrategy;
    private readonly IAlarmGenerator _alarmGenerator;
    private readonly ITelemetryBroadcaster _telemetryBroadcaster;
    private readonly PendingConfigChangeQueue _configQueue;
    private readonly ILogger<EngineBackgroundService> _logger;

    private readonly Dictionary<Guid, FacilityEngineSlot> _facilitySlots = new();
    private readonly object _slotsLock = new();

    public EngineBackgroundService(
        IDataSource dataSource,
        IDecisionStrategy decisionStrategy,
        IAlarmGenerator alarmGenerator,
        ITelemetryBroadcaster telemetryBroadcaster,
        PendingConfigChangeQueue configQueue,
        ILogger<EngineBackgroundService> logger)
    {
        _dataSource = dataSource;
        _decisionStrategy = decisionStrategy;
        _alarmGenerator = alarmGenerator;
        _telemetryBroadcaster = telemetryBroadcaster;
        _configQueue = configQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Engine tick failed");
            }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // 1. Apply pending config changes at the START of each tick.
        ApplyConfigChanges();

        // 2. Fetch Telemetry State from Data Source
        var telemetry = await _dataSource.GetCurrentStateAsync();
        if (telemetry is null)
            return;

        // 3. Snapshot the facilities to evaluate this tick.
        IReadOnlyList<FacilityEngineSlot> slots;
        lock (_slotsLock)
        {
            slots = _facilitySlots.Values.ToList();
        }

        // 💡 Registered Slots မရှိသေးလျှင်လည်း Default Empty Decision ဖြင့် Standard Broadcast 100ms မပြတ် လွှင့်ပေးမည်
        if (slots.Count == 0)
        {
            var emptyDecision = _decisionStrategy.Evaluate(telemetry, Array.Empty<Load>());

            await _telemetryBroadcaster.BroadcastTickAsync(
                Guid.Empty,
                telemetry,
                emptyDecision,
                Enumerable.Empty<AlarmEvent>(),
                ct);
            return;
        }

        foreach (var slot in slots)
        {
            await EvaluateFacilityAsync(slot, telemetry, ct);
        }
    }

    private async Task EvaluateFacilityAsync(
        FacilityEngineSlot slot,
        GridState telemetry,
        CancellationToken ct)
    {
        var snapshot = slot.ReadState();

        var decision = _decisionStrategy.Evaluate(telemetry, snapshot.Loads);
        var alarms = _alarmGenerator.GenerateAlarms(telemetry);

        foreach (var action in decision.RelayDecisions)
        {
            _logger.LogDebug(
                "Relay {Relay} -> {Action} ({Reason})",
                action.RelayAddress,
                action.Energize ? "energize" : "de-energize",
                action.Reason);

            await _dataSource.WriteRelayAsync(action.RelayAddress, action.Energize);
        }

        var alarmEvents = alarms.Select(a => new AlarmEvent(
            a.AlarmCode,
            a.Severity,
            a.Message,
            a.CreatedAtUtc
        ));

        await _telemetryBroadcaster.BroadcastTickAsync(
            snapshot.FacilityId,
            telemetry,
            decision,
            alarmEvents,
            ct);
    }

    private void ApplyConfigChanges()
    {
        var changes = _configQueue.DrainAll();
        if (changes.Count == 0)
            return;

        foreach (var facilityGroup in changes.GroupBy(c => c.FacilityId))
        {
            var facilityId = facilityGroup.Key;

            lock (_slotsLock)
            {
                if (!_facilitySlots.TryGetValue(facilityId, out var slot))
                {
                    slot = new FacilityEngineSlot(facilityId);
                    _facilitySlots[facilityId] = slot;
                }

                var current = slot.ReadState();
                var newState = FoldChanges(current, facilityGroup.ToList());
                slot.Publish(newState);
            }
        }
    }

    private static EngineState FoldChanges(EngineState current, IReadOnlyList<ConfigChange> changes)
    {
        var loads = new List<Load>(current.Loads);
        var rules = new List<Rule>(current.Rules);

        foreach (var change in changes)
        {
            switch (change)
            {
                case LoadChanged loadChanged:
                    loads.RemoveAll(l => l.Id == loadChanged.UpdatedLoad.Id);
                    loads.Add(loadChanged.UpdatedLoad);
                    break;

                case LoadRemoved loadRemoved:
                    loads.RemoveAll(l => l.Id == loadRemoved.LoadId);
                    break;

                case RuleChanged ruleChanged:
                    rules.RemoveAll(r => r.Id == ruleChanged.UpdatedRule.Id);
                    rules.Add(ruleChanged.UpdatedRule);
                    break;
            }
        }

        return new EngineState(
            loads,
            rules,
            current.CooldownStates,
            current.FacilityId,
            current.Version + 1);
    }
}

internal sealed class FacilityEngineSlot
{
    private EngineState _currentState;

    public FacilityEngineSlot(Guid facilityId)
    {
        _currentState = EngineState.Empty(facilityId);
    }

    public EngineState ReadState() => Volatile.Read(ref _currentState);

    public void Publish(EngineState newState) =>
        Interlocked.Exchange(ref _currentState, newState);
}