using System.Collections.Concurrent;
using BlackoutGuard.Domain.Entities;

namespace BlackoutGuard.Infrastructure.Engine;

public abstract record ConfigChange(Guid FacilityId, DateTime EnqueuedAt);

public sealed record LoadChanged(Guid FacilityId, DateTime EnqueuedAt, Load UpdatedLoad)
    : ConfigChange(FacilityId, EnqueuedAt);

public sealed record LoadRemoved(Guid FacilityId, DateTime EnqueuedAt, Guid LoadId)
    : ConfigChange(FacilityId, EnqueuedAt);

public sealed record RuleChanged(Guid FacilityId, DateTime EnqueuedAt, Rule UpdatedRule)
    : ConfigChange(FacilityId, EnqueuedAt);

public sealed class PendingConfigChangeQueue
{
    private ConcurrentQueue<ConfigChange> _queue = new();

    public void Enqueue(ConfigChange change)
    {
        _queue.Enqueue(change);
    }

    public IReadOnlyList<ConfigChange> DrainAll()
    {
        var drained = Interlocked.Exchange(ref _queue, new ConcurrentQueue<ConfigChange>());
        return drained.ToArray();
    }

    public int Count => _queue.Count;
}
