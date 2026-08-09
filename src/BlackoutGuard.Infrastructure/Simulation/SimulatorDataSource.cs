using System.Collections.Concurrent;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.Services;
using Microsoft.Extensions.Logging;

namespace BlackoutGuard.Infrastructure.Simulation;

public class SimulatorDataSource : IDataSource
{
    private readonly object _lock = new();
    private readonly ILogger<SimulatorDataSource> _logger;
    private GridState? _currentState;
    private readonly ConcurrentDictionary<int, bool> _relayStates = new();
    private Action<GridState>? _dataCallback;
    private volatile bool _isConnected;

    public bool IsConnected => _isConnected;

    public SimulatorDataSource(ILogger<SimulatorDataSource> logger)
    {
        _logger = logger;
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        _isConnected = true;
        _logger.LogDebug("SimulatorDataSource connected");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        _logger.LogDebug("SimulatorDataSource disconnected");
        return Task.CompletedTask;
    }

    public void UpdateSimulatedTelemetry(GridState newState)
    {
        lock (_lock)
        {
            _currentState = newState;
        }
    }

    public Task<GridState?> GetCurrentStateAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_currentState);
        }
    }

    public Task<IReadOnlyList<LoadState>> GetLoadsAsync()
    {
        var loads = _relayStates
            .Select(kvp => new LoadState { RelayAddress = kvp.Key, IsEnergized = kvp.Value })
            .ToList()
            .AsReadOnly();

        return Task.FromResult((IReadOnlyList<LoadState>)loads);
    }

    public Task<bool> WriteRelayAsync(int relayAddress, bool energize)
    {
        _relayStates[relayAddress] = energize;
        _logger.LogDebug("Relay {RelayAddress} set to {State}", relayAddress, energize ? "energized" : "de-energized");
        return Task.FromResult(true);
    }

    public void OnDataReceived(Action<GridState> callback)
    {
        _dataCallback = callback;
    }
}
