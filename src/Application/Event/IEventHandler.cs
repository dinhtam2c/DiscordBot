using Domain.Core;

namespace Application.Event;

public interface IEventHandler
{
    Task HandleAsync(IEvent @event, CancellationToken ct);
}

public interface IEventHandler<in TEvent> : IEventHandler
    where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);

    Task IEventHandler.HandleAsync(IEvent @event, CancellationToken ct)
    {
        return HandleAsync((TEvent)@event, ct);
    }

}
