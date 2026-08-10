namespace Infrastructure.Persistence;

public sealed class MemoryPersistenceContext
{
    public AggregateTracker Tracker { get; } = new();
}
