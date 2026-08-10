using Application.Discord.Interactions.Command;

namespace Modules.Music.Application.Commands.Laviai;

internal sealed class LaviaiCommand : ICommandDefinition
{
    public string Name => "laviai";

    public string Description => "LAVIAI";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
