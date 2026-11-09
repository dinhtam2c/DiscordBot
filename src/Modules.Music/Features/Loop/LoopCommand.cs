using Application.Discord.Interactions.Command;
using Modules.Music.Players;

namespace Modules.Music.Features.Loop;

internal class LoopCommand : ICommandDefinition
{
    public string Name => "loop";

    public string Description => "Set the loop mode."; 

    public IReadOnlyList<CommandOptionDefinition> Options => [
        new CommandOptionDefinition(
            Name: "mode",
            Description: "The loop mode to set.",
            Type: CommandOptionType.String,
            Required: true,
            Choices:
            [
                new("Off", LoopMode.None.ToString()),
                new("Track", LoopMode.Track.ToString()),
                new("Queue", LoopMode.Queue.ToString())
            ]
        )
    ];
}
