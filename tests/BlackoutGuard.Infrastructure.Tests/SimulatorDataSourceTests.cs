using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Infrastructure.Simulation;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlackoutGuard.Infrastructure.Tests.Simulation;

public class SimulatorDataSourceTests
{
    [Fact]
    public async Task Connect_ShouldSetIsConnectedToTrue()
    {
        var ds = CreateDataSource();
        Assert.False(ds.IsConnected);
        await ds.ConnectAsync();
        Assert.True(ds.IsConnected);
    }

    [Fact]
    public async Task Disconnect_ShouldSetIsConnectedToFalse()
    {
        var ds = CreateDataSource();
        await ds.ConnectAsync();
        Assert.True(ds.IsConnected);
        await ds.DisconnectAsync();
        Assert.False(ds.IsConnected);
    }

    [Fact]
    public async Task GetCurrentStateAsync_ShouldReturnUpdatedTelemetry()
    {
        var ds = CreateDataSource();
        var state = new GridState
        {
            Frequency = 50.0,
            Voltage = 230.0,
            TotalLoad = 150.0,
            GeneratorOn = true,
            IsBreakerTripped = false,
            TimestampUtc = DateTime.UtcNow
        };

        ds.UpdateSimulatedTelemetry(state);
        var result = await ds.GetCurrentStateAsync();

        Assert.NotNull(result);
        Assert.Equal(50.0, result!.Frequency);
        Assert.Equal(230.0, result.Voltage);
        Assert.Equal(150.0, result.TotalLoad);
        Assert.True(result.GeneratorOn);
    }

    [Fact]
    public async Task GetCurrentStateAsync_ShouldReturnNull_WhenNoTelemetrySet()
    {
        var ds = CreateDataSource();
        var result = await ds.GetCurrentStateAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task WriteRelayAsync_ShouldReturnTrue()
    {
        var ds = CreateDataSource();
        await ds.ConnectAsync();
        var result = await ds.WriteRelayAsync(3, true);
        Assert.True(result);
    }

    [Fact]
    public async Task WriteRelayAsync_ShouldUpdateInternalState()
    {
        var ds = CreateDataSource();
        await ds.ConnectAsync();

        await ds.WriteRelayAsync(1, true);
        await ds.WriteRelayAsync(2, false);

        var loads = await ds.GetLoadsAsync();
        Assert.Equal(2, loads.Count);

        var relay1 = loads.Single(l => l.RelayAddress == 1);
        Assert.True(relay1.IsEnergized);

        var relay2 = loads.Single(l => l.RelayAddress == 2);
        Assert.False(relay2.IsEnergized);
    }

    [Fact]
    public async Task WriteRelayAsync_ShouldUpdateExistingRelayState()
    {
        var ds = CreateDataSource();
        await ds.ConnectAsync();

        await ds.WriteRelayAsync(1, true);
        var loads1 = await ds.GetLoadsAsync();
        Assert.True(loads1.Single().IsEnergized);

        await ds.WriteRelayAsync(1, false);
        var loads2 = await ds.GetLoadsAsync();
        Assert.False(loads2.Single().IsEnergized);
    }

    [Fact]
    public void OnDataReceived_ShouldAcceptCallbackWithoutInvoking()
    {
        var ds = CreateDataSource();
        var invoked = false;

        ds.OnDataReceived(_ => invoked = true);

        Assert.False(invoked);
    }

    [Fact]
    public async Task TelemetryUpdate_ShouldBeThreadSafe()
    {
        var ds = CreateDataSource();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var captured = i;
            tasks.Add(Task.Run(() =>
            {
                ds.UpdateSimulatedTelemetry(new GridState
                {
                    Frequency = 50.0 + captured,
                    Voltage = 230.0,
                    TimestampUtc = DateTime.UtcNow
                });
            }));
        }

        await Task.WhenAll(tasks);

        var result = await ds.GetCurrentStateAsync();
        Assert.NotNull(result);
        Assert.True(result!.Frequency >= 50.0);
    }

    private static SimulatorDataSource CreateDataSource()
    {
        return new SimulatorDataSource(NullLogger<SimulatorDataSource>.Instance);
    }
}
