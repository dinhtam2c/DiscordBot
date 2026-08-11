using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Remove;

internal sealed class RemoveCommand : ICommandDefinition
{
    public string Name => "remove";

    public string Description => "Remove a track from the queue.";

    public IReadOnlyList<CommandOptionDefinition> Options => [
        new("index", "The index of the track to remove.", CommandOptionType.Integer)
    ];
}
