using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Tungbeo;

internal sealed class TungbeoCommand : ICommandDefinition
{
    public string Name => "tungbeo";

    public string Description => "dizz me tung beo";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
