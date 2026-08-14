using System.Collections.Concurrent;

namespace BlackoutGuard.Infrastructure.Tests.Spikes;

public record TestState(int Value, DateTime Timestamp, int Id);

public class ImmutableSnapshotSpike
{
    [Fact]
    public async Task InterlockedExchange_ProvidesAtomicSnapshotReads()
    {
        TestState sharedField = new(0, DateTime.UtcNow, 0);
        var observed = new ConcurrentBag<TestState>();
        var written = new ConcurrentBag<TestState>();
        var idCounter = 0;
        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var reader = Task.Run(async () =>
        {
            await firstWrite.Task;
            while (true)
            {
                var snapshot = Volatile.Read(ref sharedField);
                observed.Add(snapshot);

                try
                {
                    await Task.Delay(10, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        var writer = Task.Run(async () =>
        {
            while (true)
            {
                var id = Interlocked.Increment(ref idCounter);
                var state = new TestState(id * 100, DateTime.UtcNow, id);
                Interlocked.Exchange(ref sharedField, state);
                written.Add(state);
                firstWrite.TrySetResult();

                try
                {
                    await Task.Delay(30, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        await Task.WhenAll(reader, writer);

        var writtenIds = written.Select(w => w.Id).ToHashSet();
        var writtenStates = written.Select(w => (w.Value, w.Timestamp, w.Id)).ToHashSet();

        Assert.NotEmpty(observed);
        Assert.NotEmpty(written);
        Assert.True(written.Count >= 50, $"Expected many writes in 3s, got {written.Count}");

        foreach (var snapshot in observed)
        {
            Assert.Contains(snapshot.Id, writtenIds);
            Assert.Contains(
                (snapshot.Value, snapshot.Timestamp, snapshot.Id),
                writtenStates);
        }
    }
}
