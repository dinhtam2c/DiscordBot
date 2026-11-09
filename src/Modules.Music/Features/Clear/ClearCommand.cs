using Application.Discord.Interactions.Command;

namespace Modules.Music.Features.Clear;

internal sealed class ClearCommand : ICommandDefinition
{
    public string Name => "clear";

    public string Description => "Clear the current playback queue.";

    public IReadOnlyList<CommandOptionDefinition> Options => [];
}
