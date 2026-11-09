using Application.Discord.Interactions.Command;

namespace Modules.Music.Features.PlayNext;

internal class PlayNextCommand : ICommandDefinition
{
    public string Name => "playnext";

    public string Description => "Play a song next in the queue.";

    public IReadOnlyList<CommandOptionDefinition> Options => [
        new CommandOptionDefinition(
            Name: "query",
            Description: "The song to play.",
            Type: CommandOptionType.String,
            Required: true
        )
    ];
}
