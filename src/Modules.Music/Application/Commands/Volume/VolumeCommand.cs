using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Volume;

internal sealed class VolumeCommand : ICommandDefinition
{
    public string Name => "volume";

    public string Description => "Set the playback volume.";

    public IReadOnlyList<CommandOptionDefinition> Options =>
    [
        new("level", "Volume from 0 to 100 percent.", CommandOptionType.Integer),
    ];
}
