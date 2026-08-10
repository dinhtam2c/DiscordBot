using Application.Discord.Interactions.Command;

namespace Modules.Utility.Ping;

internal sealed class PingCommand : ICommandDefinition
{
    public string Name => "ping";

    public string Description => "Ping the bot.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}