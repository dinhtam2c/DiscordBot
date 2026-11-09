namespace Infrastructure.Persistence;

public sealed class InMemoryStore<TKey, TAggregate>
    where TKey : notnull
    where TAggregate : class
{
    internal sealed record Snapshot(TAggregate Aggregate, long Version);

    private readonly Dictionary<TKey, Snapshot> _entries = [];
    private long _nextVersion;

    internal Snapshot? Read(TKey key)
    {
        lock (MemoryPersistenceContext.SyncRoot)
            return _entries.GetValueOrDefault(key);
    }

    // Writes are called only by the context, while holding the transaction lock.
    internal long Save(TKey key, TAggregate snapshot)
    {
        var version = ++_nextVersion;
        _entries[key] = new Snapshot(snapshot, version);
        return version;
    }

    internal void Remove(TKey key) => _entries.Remove(key);
}
