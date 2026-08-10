namespace Application.Discord.Interactions.Dispatcher;

public sealed class InteractionHandlerResolver(IEnumerable<IInteractionRegistration> registrations)
{
    private readonly Dictionary<string, Type> _handlers = registrations.ToDictionary(x => x.Name, x => x.HandlerType, StringComparer.OrdinalIgnoreCase);

    public Type Resolve(string commandName)
    {
        if (_handlers.TryGetValue(commandName, out var handlerType))
            return handlerType;

        throw new InvalidOperationException(
            $"No interaction handler registered for '{commandName}'.");
    }
}