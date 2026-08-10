using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Stop;

internal sealed class StopCommand : ICommandDefinition
{
    public string Name => "stop";

    public string Description => "Stop and clear the current queue.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
