using Application.Discord.Interactions.Command;

namespace Modules.Utility.Help;

internal sealed class HelpCommand : ICommandDefinition
{
    public string Name => "help";

    public string Description => "Display help information.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
