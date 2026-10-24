using Application.Discord.Interactions.Dispatcher;

namespace Application.Discord.Interactions.Command;

public interface IInteractionCommandSource
{
    IReadOnlyCollection<ICommandDefinition> Commands { get; }

    IReadOnlyCollection<IInteractionRegistration> Registrations { get; }
}
