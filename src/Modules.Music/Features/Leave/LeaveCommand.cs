using Application.Discord.Interactions.Command;

namespace Modules.Music.Features.Leave;

internal class LeaveCommand : ICommandDefinition
{
    public string Name => "leave";

    public string Description => "Leave the voice channel.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
