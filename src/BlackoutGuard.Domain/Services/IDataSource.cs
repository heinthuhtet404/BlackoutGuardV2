using BlackoutGuard.Domain.Entities;

namespace BlackoutGuard.Domain.Services;

public interface IDataSource
{
    Task<GridState?> GetCurrentStateAsync();
    Task<IReadOnlyList<LoadState>> GetLoadsAsync();

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    bool IsConnected { get; }

    Task<bool> WriteRelayAsync(int relayAddress, bool energize);

    void OnDataReceived(Action<GridState> callback);
}

public class LoadState
{
    public int RelayAddress { get; set; }
    public bool IsEnergized { get; set; }
}
