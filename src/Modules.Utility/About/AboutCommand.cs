using Application.Discord.Interactions.Command;

namespace Modules.Utility.About;

internal sealed class AboutCommand : ICommandDefinition
{
    public string Name => "about";

    public string Description => "Get information about the bot.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
