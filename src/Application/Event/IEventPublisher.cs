using Domain.Core;

namespace Application.Event;

public interface IEventPublisher
{
    Task PublishAsync(IEvent @event, CancellationToken ct);
}
