using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Shuffle;

internal sealed class ShuffleCommand : ICommandDefinition
{
    public string Name => "shuffle";

    public string Description => "Shuffle the current playback queue.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
