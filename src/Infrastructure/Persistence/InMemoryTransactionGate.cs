namespace Infrastructure.Persistence;

internal static class InMemoryTransactionGate
{
    internal static object SyncRoot { get; } = new();
}
