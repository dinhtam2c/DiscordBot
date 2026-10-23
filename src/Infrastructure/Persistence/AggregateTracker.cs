using Domain.Core;

namespace Infrastructure.Persistence;

public sealed class AggregateTracker
{
    private readonly Dictionary<TrackingKey, TrackedEntry> _entries = [];

    public bool TryGet<TAggregate>(object store, object key, out TAggregate? aggregate)
        where TAggregate : AggregateRoot
    {
        if (_entries.TryGetValue(new TrackingKey(store, key), out var entry) && !entry.IsDeleted)
        {
            aggregate = (TAggregate?)entry.Aggregate;
            return aggregate is not null;
        }

        aggregate = null;
        return false;
    }

    internal bool IsDeleted(object store, object key)
        => _entries.TryGetValue(new TrackingKey(store, key), out var entry) && entry.IsDeleted;

    internal bool TryGetEntry(object store, object key, out TrackedEntry entry)
        => _entries.TryGetValue(new TrackingKey(store, key), out entry!);

    public void Track<TAggregate>(
        object store,
        object key,
        TAggregate aggregate,
        Func<bool> canApply,
        Func<bool> apply,
        long? version = null)
        where TAggregate : AggregateRoot
    {
        _entries[new TrackingKey(store, key)] = new TrackedEntry(aggregate, canApply, apply, version, isDeleted: false);
    }

    internal void TrackDeleted(object store, object key, Func<bool> canApply, Func<bool> apply)
        => _entries[new TrackingKey(store, key)] = new TrackedEntry(null, canApply, apply, null, isDeleted: true);

    internal void MarkDeleted(object store, object key, Func<bool> canApply, Func<bool> apply)
    {
        var trackingKey = new TrackingKey(store, key);
        if (_entries.TryGetValue(trackingKey, out var entry) && entry.IsDeleted)
            return;

        if (entry is not null && entry.Version is null)
        {
            _entries.Remove(trackingKey);
            return;
        }

        _entries[trackingKey] = new TrackedEntry(entry?.Aggregate, canApply, apply, entry?.Version, isDeleted: true);
    }

    internal IReadOnlyCollection<TrackedEntry> Entries => _entries.Values;
    internal void Clear() => _entries.Clear();

    internal IReadOnlyCollection<AggregateRoot> TrackedAggregates =>
        _entries.Values
            .Where(entry => entry.Aggregate is not null)
            .Select(entry => entry.Aggregate!)
            .ToArray();

    internal readonly record struct TrackingKey(object Store, object Key);

    internal sealed class TrackedEntry(
        AggregateRoot? aggregate,
        Func<bool> canApply,
        Func<bool> apply,
        long? version,
        bool isDeleted)
    {
        public AggregateRoot? Aggregate { get; } = aggregate;
        public Func<bool> CanApply { get; } = canApply;
        public Func<bool> Apply { get; } = apply;
        public long? Version { get; } = version;
        public bool IsDeleted { get; } = isDeleted;
    }
}
