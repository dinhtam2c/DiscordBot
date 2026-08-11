using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Move;

internal sealed class MoveCommand : ICommandDefinition
{
    public string Name => "move";

    public string Description => "Move a track in the queue.";

    public IReadOnlyList<CommandOptionDefinition> Options => [
        new("index", "The index of the track to move.", CommandOptionType.Integer),
        new("new_index", "The new index of the track.", CommandOptionType.Integer)
    ];
}
