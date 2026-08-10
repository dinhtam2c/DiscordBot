namespace Application.Discord.Interactions.Command;

public interface ICommandDefinition
{
    string Name { get; }

    string Description { get; }

    IReadOnlyList<CommandOptionDefinition> Options { get; }
}
