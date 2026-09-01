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

    // ⚡ Config Variable များကို ထည့်သွင်းခြင်း (Default values ထားပေးထားသည်)
    private bool _gridOnline = true;
    private double _solarCapacityKw = 50.0;
    private double _generatorCapacityKw = 100.0;

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

    // ⚡ 1. Controller မှ GET /simulator/config ခေါ်သည့်အခါ ပြန်ပေးမည့် Method
    public SimulatorConfigModel GetConfig()
    {
        lock (_lock)
        {
            return new SimulatorConfigModel
            {
                GridOnline = _gridOnline,
                SolarCapacityKw = _solarCapacityKw,
                GeneratorCapacityKw = _generatorCapacityKw
            };
        }
    }

    // ⚡ 2. Controller မှ POST /simulator/config ခေါ်သည့်အခါ Update လုပ်ပေးမည့် Method
    public void UpdateConfig(bool gridOnline, double solarCapacityKw, double generatorCapacityKw)
    {
        lock (_lock)
        {
            _gridOnline = gridOnline;
            _solarCapacityKw = solarCapacityKw;
            _generatorCapacityKw = generatorCapacityKw;
        }

        _logger.LogInformation("Simulator config updated: GridOnline={GridOnline}, Solar={Solar}kW, Gen={Gen}kW",
            gridOnline, solarCapacityKw, generatorCapacityKw);
    }

    public void UpdateSimulatedTelemetry(GridState newState)
    {
        lock (_lock)
        {
            _currentState = newState;
        }
    }

    public void UpdateTelemetry(double frequency, double voltage, double totalLoad, bool generatorOn)
    {
        lock (_lock)
        {
            _currentState = new GridState
            {
                Frequency = frequency,
                Voltage = voltage,
                TotalLoad = totalLoad,
                GeneratorOn = generatorOn,
                TimestampUtc = DateTime.UtcNow
            };
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

// ⚡ 3. Simulator Config Model DTO Class
public class SimulatorConfigModel
{
    public bool GridOnline { get; set; }
    public double SolarCapacityKw { get; set; }
    public double GeneratorCapacityKw { get; set; }
}