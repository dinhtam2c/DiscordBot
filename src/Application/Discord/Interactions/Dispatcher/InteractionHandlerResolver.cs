using Application.Discord.Interactions.Command;

namespace Application.Discord.Interactions.Dispatcher;

public sealed class InteractionHandlerResolver(
    IEnumerable<IInteractionRegistration> registrations,
    IEnumerable<IInteractionCommandSource> commandSources)
{
    private readonly Dictionary<string, Type> _handlers = registrations
        .Concat(commandSources.SelectMany(source => source.Registrations))
        .ToDictionary(x => x.Name, x => x.HandlerType, StringComparer.OrdinalIgnoreCase);

    public bool TryResolve(string commandName, out Type? handlerType)
        => _handlers.TryGetValue(commandName, out handlerType);
}
