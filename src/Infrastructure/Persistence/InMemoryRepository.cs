namespace Infrastructure.Persistence;

public abstract class InMemoryRepository<TKey, TAggregate>(
    InMemoryStore<TKey, TAggregate> store,
    MemoryPersistenceContext context)
    where TKey : notnull
    where TAggregate : class
{
    protected abstract TKey GetKey(TAggregate aggregate);
    protected abstract TAggregate Copy(TAggregate aggregate);

    protected Task<TAggregate?> GetWorkingCopyAsync(TKey key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(context.Get(store, key, Copy));
    }

    protected Task StageAddAsync(TAggregate aggregate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        context.Add(store, GetKey(aggregate), aggregate, Copy);
        return Task.CompletedTask;
    }

    protected Task StageRemoveAsync(TKey key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        context.Remove(store, key, Copy);
        return Task.CompletedTask;
    }
}
