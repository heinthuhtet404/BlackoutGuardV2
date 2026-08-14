using System.Collections.Concurrent;
using BlackoutGuard.Domain.Entities;
using BlackoutGuard.Infrastructure.Engine;

namespace BlackoutGuard.Infrastructure.Tests.Engine;

public class PendingConfigChangeQueueTests
{
    private static Load MakeLoad(int id) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        $"Load {id}",
        id,
        10.0,
        "P2",
        "auto",
        true,
        true);

    private static Rule MakeRule(int id) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "FREQ_LOW",
        47.0,
        49.5,
        30,
        true);

    [Fact]
    public void Enqueue_ThenDrainAll_ReturnsEnqueuedChange()
    {
        var queue = new PendingConfigChangeQueue();
        var facilityId = Guid.NewGuid();
        var change = new LoadChanged(facilityId, DateTime.UtcNow, MakeLoad(1));

        queue.Enqueue(change);
        var drained = queue.DrainAll();

        var single = Assert.Single(drained);
        Assert.Same(change, single);
        Assert.Equal(facilityId, single.FacilityId);
    }

    [Fact]
    public void DrainAll_EmptiesQueue_AndPreservesOrder()
    {
        var queue = new PendingConfigChangeQueue();
        var facilityId = Guid.NewGuid();
        var c1 = new LoadChanged(facilityId, DateTime.UtcNow, MakeLoad(1));
        var c2 = new RuleChanged(facilityId, DateTime.UtcNow, MakeRule(1));
        var c3 = new LoadRemoved(facilityId, DateTime.UtcNow, Guid.NewGuid());

        queue.Enqueue(c1);
        queue.Enqueue(c2);
        queue.Enqueue(c3);

        var drained = queue.DrainAll();

        Assert.Equal(3, drained.Count);
        Assert.Same(c1, drained[0]);
        Assert.Same(c2, drained[1]);
        Assert.Same(c3, drained[2]);
        Assert.Equal(0, queue.Count);
        Assert.Empty(queue.DrainAll());
    }

    [Fact]
    public async Task ConcurrentEnqueues_FromMultipleThreads_AreAllCaptured()
    {
        var queue = new PendingConfigChangeQueue();
        var facilityId = Guid.NewGuid();
        const int threads = 8;
        const int perThread = 250;

        var tasks = Enumerable.Range(0, threads).Select(threadIndex =>
            Task.Run(() =>
            {
                for (var i = 0; i < perThread; i++)
                {
                    queue.Enqueue(new LoadChanged(
                        facilityId,
                        DateTime.UtcNow,
                        MakeLoad(threadIndex * perThread + i)));
                }
            }));

        await Task.WhenAll(tasks);

        var drained = queue.DrainAll();

        Assert.Equal(threads * perThread, drained.Count);
        var loadIds = drained.OfType<LoadChanged>()
            .Select(l => l.UpdatedLoad.RelayAddress)
            .ToHashSet();
        Assert.Equal(threads * perThread, loadIds.Count);
    }

    [Fact]
    public async Task DrainAll_IsAtomic_NoLostOrDuplicatedChanges_UnderTightInterleaving()
    {
        var queue = new PendingConfigChangeQueue();
        var facilityId = Guid.NewGuid();
        var totalEnqueued = 0;
        var totalDrained = 0;
        var idCounter = 0;
        var seenIds = new ConcurrentBag<int>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var producers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            var localId = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                queue.Enqueue(new LoadChanged(
                    facilityId,
                    DateTime.UtcNow,
                    MakeLoad(Interlocked.Increment(ref idCounter))));
                Interlocked.Increment(ref totalEnqueued);

                if ((++localId & 3) == 0)
                {
                    try
                    {
                        await Task.Delay(1, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }));

        var consumer = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var drained = queue.DrainAll();
                foreach (var change in drained)
                {
                    var loadId = ((LoadChanged)change).UpdatedLoad.RelayAddress;
                    seenIds.Add(loadId);
                }
                Interlocked.Add(ref totalDrained, drained.Count);

                try
                {
                    await Task.Delay(2, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        await Task.WhenAll(producers.Append(consumer));

        var finalDrain = queue.DrainAll();
        foreach (var change in finalDrain)
        {
            var loadId = ((LoadChanged)change).UpdatedLoad.RelayAddress;
            seenIds.Add(loadId);
        }
        Interlocked.Add(ref totalDrained, finalDrain.Count);

        Assert.True(totalEnqueued > 100, $"Expected meaningful traffic, got {totalEnqueued}");
        Assert.Equal(totalEnqueued, totalDrained);
        Assert.Equal(totalDrained, seenIds.Count);
        Assert.Equal(seenIds.Distinct().Count(), seenIds.Count);
    }

    [Fact]
    public void ConfigChange_Records_HaveCorrectHierarchy()
    {
        var facilityId = Guid.NewGuid();
        var enqueuedAt = DateTime.UtcNow;
        var load = MakeLoad(1);
        var rule = MakeRule(1);
        var loadId = Guid.NewGuid();

        ConfigChange loadChanged = new LoadChanged(facilityId, enqueuedAt, load);
        ConfigChange loadRemoved = new LoadRemoved(facilityId, enqueuedAt, loadId);
        ConfigChange ruleChanged = new RuleChanged(facilityId, enqueuedAt, rule);

        Assert.IsType<LoadChanged>(loadChanged);
        Assert.IsType<LoadRemoved>(loadRemoved);
        Assert.IsType<RuleChanged>(ruleChanged);
        Assert.Equal(facilityId, loadChanged.FacilityId);
        Assert.Equal(enqueuedAt, loadChanged.EnqueuedAt);
        Assert.Same(load, ((LoadChanged)loadChanged).UpdatedLoad);
        Assert.Equal(loadId, ((LoadRemoved)loadRemoved).LoadId);
        Assert.Same(rule, ((RuleChanged)ruleChanged).UpdatedRule);
    }
}
