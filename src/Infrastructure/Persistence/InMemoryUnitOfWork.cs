using Application.Abstractions;
using Application.Event;

namespace Infrastructure.Persistence;

internal sealed class InMemoryUnitOfWork(
    MemoryPersistenceContext context,
    IEventPublisher publisher) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var aggregates = context.CommitChanges();
        foreach (var aggregate in aggregates)
        {
            foreach (var @event in aggregate.DomainEvents.ToArray())
            {
                await publisher.PublishAsync(@event, ct);
            }
            aggregate.ClearDomainEvents();
        }
    }
}
