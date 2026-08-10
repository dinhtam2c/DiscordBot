using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Join;

internal sealed class JoinCommand : ICommandDefinition
{
    public string Name => "join";

    public string Description => "Join the voice channel.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
