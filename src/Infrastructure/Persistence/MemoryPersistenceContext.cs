namespace Infrastructure.Persistence;

public sealed class MemoryPersistenceContext
{
    internal static object SyncRoot { get; } = new();

    private readonly Dictionary<(object Store, object Key), IEntry> _entries = [];

    internal TAggregate? Get<TKey, TAggregate>(
        InMemoryStore<TKey, TAggregate> store, TKey key, Func<TAggregate, TAggregate> copy)
        where TKey : notnull
        where TAggregate : class
    {
        if (_entries.TryGetValue((store, key), out var tracked))
        {
            var entry = (Entry<TKey, TAggregate>)tracked;
            return entry.Deleted ? null : entry.Aggregate;
        }

        var stored = store.Read(key);
        if (stored is null)
            return null;

        var workingCopy = copy(stored.Aggregate);
        _entries.Add((store, key), new Entry<TKey, TAggregate>(store, key, workingCopy, stored.Version, copy));
        return workingCopy;
    }

    internal void Add<TKey, TAggregate>(
        InMemoryStore<TKey, TAggregate> store, TKey key, TAggregate aggregate, Func<TAggregate, TAggregate> copy)
        where TKey : notnull
        where TAggregate : class
    {
        if (_entries.TryGetValue((store, key), out var tracked))
        {
            var entry = (Entry<TKey, TAggregate>)tracked;
            if (!entry.Deleted)
                throw new InvalidOperationException($"Aggregate '{key}' is already tracked in this context.");

            _entries[(store, key)] = new Entry<TKey, TAggregate>(store, key, aggregate, entry.Version, copy);
            return;
        }

        if (store.Read(key) is not null)
            throw new InvalidOperationException($"Aggregate '{key}' already exists.");

        _entries.Add((store, key), new Entry<TKey, TAggregate>(store, key, aggregate, null, copy));
    }

    internal void Remove<TKey, TAggregate>(
        InMemoryStore<TKey, TAggregate> store, TKey key, Func<TAggregate, TAggregate> copy)
        where TKey : notnull
        where TAggregate : class
    {
        if (!_entries.TryGetValue((store, key), out var tracked))
        {
            var stored = store.Read(key);
            if (stored is null)
                return;

            tracked = new Entry<TKey, TAggregate>(store, key, null, stored.Version, copy);
            _entries.Add((store, key), tracked);
        }

        var entry = (Entry<TKey, TAggregate>)tracked;
        if (entry.Version is null)
            _entries.Remove((store, key)); // Adding then removing before save has no effect on the store.
        else
            entry.Deleted = true;
    }

    public void SaveChanges()
    {
        lock (SyncRoot)
        {
            foreach (var entry in _entries.Values)
                entry.Validate();

            // Copy everything before changing any store, so a copy failure cannot partially save.
            foreach (var entry in _entries.Values)
                entry.Prepare();

            foreach (var entry in _entries.Values)
                entry.Apply();

            foreach (var key in _entries.Where(pair => pair.Value.Deleted).Select(pair => pair.Key).ToArray())
                _entries.Remove(key);
        }
    }

    private interface IEntry
    {
        bool Deleted { get; }
        void Validate();
        void Prepare();
        void Apply();
    }

    private sealed class Entry<TKey, TAggregate>(
        InMemoryStore<TKey, TAggregate> store,
        TKey key,
        TAggregate? aggregate,
        long? version,
        Func<TAggregate, TAggregate> copy) : IEntry
        where TKey : notnull
        where TAggregate : class
    {
        private TAggregate? _snapshot;

        public TAggregate? Aggregate { get; } = aggregate;
        public long? Version { get; private set; } = version;
        public bool Deleted { get; set; }

        public void Validate()
        {
            if (store.Read(key)?.Version != Version)
                throw new InvalidOperationException($"Cannot save in-memory aggregate '{key}' because it changed concurrently or already exists.");
        }

        public void Prepare() => _snapshot = Deleted ? null : copy(Aggregate!);

        public void Apply()
        {
            if (Deleted)
                store.Remove(key);
            else
                Version = store.Save(key, _snapshot!);

            _snapshot = null;
        }
    }
}
