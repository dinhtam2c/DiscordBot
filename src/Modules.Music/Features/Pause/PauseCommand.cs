using Application.Discord.Interactions.Command;

namespace Modules.Music.Features.Pause;

internal sealed class PauseCommand : ICommandDefinition
{
    public string Name => "pause";

    public string Description => "Pause the current track.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
