namespace Infrastructure.Persistence;

public sealed class MemoryPersistenceContext
{
    public AggregateTracker Tracker { get; } = new();

    public IReadOnlyCollection<Domain.Core.AggregateRoot> CommitChanges()
    {
        lock (InMemoryTransactionGate.SyncRoot)
        {
            var entries = Tracker.Entries.ToArray();
            if (entries.Any(entry => !entry.CanApply()))
                throw new InvalidOperationException("An in-memory aggregate changed concurrently or already exists.");

            foreach (var entry in entries)
            {
                if (!entry.Apply())
                    throw new InvalidOperationException("An in-memory aggregate could not be committed.");
            }

            var aggregates = Tracker.TrackedAggregates;
            Tracker.Clear();
            return aggregates;
        }
    }
}
