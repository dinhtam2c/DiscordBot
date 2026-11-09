using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Skip;

[InteractionHandler("skip")]
internal sealed class SkipHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var result = await players.ExecuteAsync(guildId, new SkipTrack(), ct);
        var response = result.Before?.CurrentTrack is { } skipped
            ? $"?? Skipped: {skipped.Title}"
            : "There is no track currently playing.";
        await context.Responder.RespondAsync(InteractionResponse.Text(response), ct);
    }
}
