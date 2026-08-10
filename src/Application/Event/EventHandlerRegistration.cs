namespace Application.Event;

internal sealed record EventHandlerRegistration(Type EventType, Type HandlerType);
