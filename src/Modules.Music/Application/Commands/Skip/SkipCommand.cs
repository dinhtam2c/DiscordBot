using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Skip;

internal class SkipCommand : ICommandDefinition
{
    public string Name => "skip";

    public string Description => "Skip the current track.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
