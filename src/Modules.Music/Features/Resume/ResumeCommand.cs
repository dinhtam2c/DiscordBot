using Application.Discord.Interactions.Command;

namespace Modules.Music.Features.Resume;

internal sealed class ResumeCommand : ICommandDefinition
{
    public string Name => "resume";

    public string Description => "Resume the paused track.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
