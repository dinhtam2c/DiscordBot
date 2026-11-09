using Application.Discord.Interactions.Command;

namespace Modules.Music.Features.Skip;

internal class SkipCommand : ICommandDefinition
{
    public string Name => "skip";

    public string Description => "Skip the current track.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
