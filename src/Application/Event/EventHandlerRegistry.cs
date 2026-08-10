namespace Application.Event;

internal class EventHandlerRegistry(IEnumerable<EventHandlerRegistration> registrations)
{
    private readonly Dictionary<Type, Type[]> _handlers
        = registrations
            .GroupBy(x => x.EventType)
            .ToDictionary(x => x.Key, x => x.Select(y => y.HandlerType).ToArray());

    public IReadOnlyList<Type> GetHandlers(Type eventType)
    {
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            return handlers;
        }
        return Array.Empty<Type>();
    }
}
