using Domain.Core;

namespace Infrastructure.Persistence;

public sealed class InMemoryStore<TKey, TAggregate>
    where TKey : notnull
    where TAggregate : AggregateRoot
{
    private sealed record Entry(TAggregate Aggregate, long Version);
    private readonly Dictionary<TKey, Entry> _entries = [];

    internal bool TryRead(TKey key, out TAggregate? aggregate, out long version)
    {
        lock (InMemoryTransactionGate.SyncRoot)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                aggregate = entry.Aggregate;
                version = entry.Version;
                return true;
            }
        }

        aggregate = null;
        version = default;
        return false;
    }

    internal bool IsMissing(TKey key)
    {
        lock (InMemoryTransactionGate.SyncRoot)
            return !_entries.ContainsKey(key);
    }

    internal bool HasVersion(TKey key, long expectedVersion)
    {
        lock (InMemoryTransactionGate.SyncRoot)
            return _entries.TryGetValue(key, out var entry) && entry.Version == expectedVersion;
    }

    internal bool TryAdd(TKey key, TAggregate aggregate, Func<TAggregate, TAggregate> copy)
    {
        lock (InMemoryTransactionGate.SyncRoot)
            return _entries.TryAdd(key, new Entry(copy(aggregate), 1));
    }

    internal bool TryReplace(TKey key, long expectedVersion, TAggregate aggregate, Func<TAggregate, TAggregate> copy)
    {
        lock (InMemoryTransactionGate.SyncRoot)
        {
            if (!_entries.TryGetValue(key, out var entry) || entry.Version != expectedVersion)
                return false;

            _entries[key] = new Entry(copy(aggregate), entry.Version + 1);
            return true;
        }
    }

    internal bool TryRemove(TKey key, long expectedVersion)
    {
        lock (InMemoryTransactionGate.SyncRoot)
        {
            if (!_entries.TryGetValue(key, out var entry) || entry.Version != expectedVersion)
                return false;

            return _entries.Remove(key);
        }
    }
}
