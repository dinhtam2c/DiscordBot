using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Queue;

internal sealed class QueueCommand : ICommandDefinition
{
    public string Name => "queue";

    public string Description => "Show the current playback queue.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
