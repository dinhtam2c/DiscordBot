using Application.Abstractions;
using Application.Event;

namespace Infrastructure.Persistence;

internal sealed class UnitOfWork(
    MemoryPersistenceContext context,
    IEventPublisher publisher) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct)
    {
        foreach (var aggregate in context.Tracker.GetTracked())
        {
            foreach (var @event in aggregate.DomainEvents)
            {
                await publisher.PublishAsync(@event, ct);
            }
            aggregate.ClearDomainEvents();
        }
    }
}
