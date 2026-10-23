using Domain.Core;

namespace Infrastructure.Persistence;

public abstract class InMemoryRepository<TKey, TAggregate>(
    InMemoryStore<TKey, TAggregate> store,
    MemoryPersistenceContext context)
    where TKey : notnull
    where TAggregate : AggregateRoot
{
    protected abstract TKey GetKey(TAggregate aggregate);

    protected abstract TAggregate Copy(TAggregate aggregate);

    protected Task<TAggregate?> GetWorkingCopyAsync(TKey key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (context.Tracker.IsDeleted(store, key))
            return Task.FromResult<TAggregate?>(null);

        if (context.Tracker.TryGet(store, key, out TAggregate? tracked))
            return Task.FromResult(tracked);

        if (!store.TryRead(key, out var stored, out var version) || stored is null)
            return Task.FromResult<TAggregate?>(null);

        var workingCopy = Copy(stored);
        context.Tracker.Track(store, key, workingCopy,
            () => store.HasVersion(key, version),
            () => store.TryReplace(key, version, workingCopy, Copy),
            version);

        return Task.FromResult<TAggregate?>(workingCopy);
    }

    protected Task StageAddAsync(TAggregate aggregate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = GetKey(aggregate);

        if (context.Tracker.TryGet<TAggregate>(store, key, out _))
            throw new InvalidOperationException($"Aggregate '{key}' is already tracked in this context.");

        if (store.TryRead(key, out _, out _))
            throw new InvalidOperationException($"Aggregate '{key}' already exists.");

        context.Tracker.Track(store, key, aggregate,
            () => store.IsMissing(key),
            () => store.TryAdd(key, aggregate, Copy));
        return Task.CompletedTask;
    }

    protected Task StageRemoveAsync(TKey key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (context.Tracker.TryGetEntry(store, key, out var trackedEntry))
        {
            context.Tracker.MarkDeleted(store, key,
                () => trackedEntry.Version is { } version && store.HasVersion(key, version),
                () => trackedEntry.Version is { } version && store.TryRemove(key, version));
            return Task.CompletedTask;
        }

        if (!store.TryRead(key, out _, out var version))
            return Task.CompletedTask;

        context.Tracker.TrackDeleted(store, key,
            () => store.HasVersion(key, version),
            () => store.TryRemove(key, version));
        return Task.CompletedTask;
    }
}
