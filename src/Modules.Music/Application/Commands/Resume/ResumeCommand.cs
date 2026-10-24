using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Resume;

internal sealed class ResumeCommand : ICommandDefinition
{
    public string Name => "resume";

    public string Description => "Resume the paused track.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
