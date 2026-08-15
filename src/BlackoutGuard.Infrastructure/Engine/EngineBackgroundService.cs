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

    // Single loop iterating all facilities (choice: one loop, one timer).
    // Rationale: fewer moving parts than N per-facility services, config
    // changes are naturally batched per tick, and per-facility parallelism
    // can be introduced later with partitioned channels if profiling
    // shows a need for it.
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

        // 2. Snapshot the facilities to evaluate this tick.
        IReadOnlyList<FacilityEngineSlot> slots;
        lock (_slotsLock)
        {
            slots = _facilitySlots.Values.ToList();
        }

        if (slots.Count == 0)
            return;

        // 3. Telemetry (single shared source for now; per-facility sources
        //    are a Phase 5 concern).
        var telemetry = await _dataSource.GetCurrentStateAsync();
        if (telemetry is null)
            return;

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

        // 4. Decision engine + alarms (pure Domain logic).
        var decisions = _decisionStrategy.Evaluate(snapshot, telemetry);
        var alarms = _alarmGenerator.Evaluate(snapshot, telemetry);

        // 5. Relay writes — the ONLY place WriteRelayAsync is permitted.
        foreach (var decision in decisions)
        {
            _logger.LogDebug(
                "Relay {Relay} -> {Action} ({Reason})",
                decision.RelayAddress,
                decision.Energize ? "energize" : "de-energize",
                decision.Reason);

            await _dataSource.WriteRelayAsync(decision.RelayAddress, decision.Energize);
        }

        // 6. Audit/alarm events — enqueue to BatchedEventPublisher.
        // TODO (Phase 5): `_batchedEventPublisher.Enqueue(snapshot.FacilityId, decisions, alarms);`
        // BatchedEventPublisher is ported from V1 in a later task.

        // 7. SignalR broadcast — real broadcaster wired in Task 5.2.
        var decisionPayload = decisions.Count > 0
            ? LoadSheddingDecision.Create(decisions)
            : LoadSheddingDecision.None;

        await _telemetryBroadcaster.BroadcastTickAsync(
            snapshot.FacilityId,
            telemetry,
            decisionPayload,
            alarms,
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
    private volatile EngineState _currentState;

    public FacilityEngineSlot(Guid facilityId)
    {
        _currentState = EngineState.Empty(facilityId);
    }

    public EngineState ReadState() => Volatile.Read(ref _currentState);

    public void Publish(EngineState newState) =>
        Interlocked.Exchange(ref _currentState, newState);
}
