using Application.Abstractions;

namespace Infrastructure.Persistence;

internal sealed class InMemoryUnitOfWork(MemoryPersistenceContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        context.SaveChanges();
        return Task.CompletedTask;
    }
}
