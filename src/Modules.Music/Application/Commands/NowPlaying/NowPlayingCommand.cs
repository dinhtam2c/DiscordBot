using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.NowPlaying;

internal sealed class NowPlayingCommand : ICommandDefinition
{
    public string Name => "nowplaying";

    public string Description => "Show the current track.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
