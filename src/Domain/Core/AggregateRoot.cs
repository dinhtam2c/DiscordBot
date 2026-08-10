namespace Domain.Core;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events.AsReadOnly();

    protected void Raise(IDomainEvent e)
    {
        _events.Add(e);
    }

    public void ClearDomainEvents()
    {
        _events.Clear();
    }
}
