using Domain.Core;

namespace Infrastructure.Persistence;

public sealed class AggregateTracker
{
    private readonly HashSet<AggregateRoot> _aggregates = [];

    public void Track(AggregateRoot aggregate)
    {
        _aggregates.Add(aggregate);
    }

    public IReadOnlyCollection<AggregateRoot> GetTracked()
        => _aggregates;
}
