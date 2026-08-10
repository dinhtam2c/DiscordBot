using Domain.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Event;

internal class EventPublisher(
    IServiceProvider services,
    EventHandlerRegistry registry,
    ILogger<EventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync(IEvent @event, CancellationToken ct)
    {
        var eventType = @event.GetType();

        foreach (var handlerType in registry.GetHandlers(eventType))
        {
            try
            {
                var handler = services.GetRequiredService(handlerType);
                await ((IEventHandler)handler).HandleAsync(@event, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Error handling event {EventType} with handler {HandlerType}",
                    @event.GetType().Name,
                    handlerType.Name);
            }
        }
    }
}
