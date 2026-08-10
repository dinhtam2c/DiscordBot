using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Play;

internal class PlayCommand : ICommandDefinition
{
    public string Name => "play";

    public string Description => "Play a song in the voice channel.";

    public IReadOnlyList<CommandOptionDefinition> Options => [
        new CommandOptionDefinition(
            Name: "query",
            Description: "The song to play.",
            Type: CommandOptionType.String,
            Required: true
        )
    ];
}
