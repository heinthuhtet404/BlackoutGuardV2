using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Domain.Services;

namespace BlackoutGuard.Infrastructure.DataSources;

public class SimulationDataSource : IDataSource
{
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task<GridState?> GetCurrentStateAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<LoadState>> GetLoadsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<bool> WriteRelayAsync(int relayAddress, bool energize)
    {
        throw new NotImplementedException();
    }

    public void OnDataReceived(Action<GridState> callback)
    {
        throw new NotImplementedException();
    }
}
