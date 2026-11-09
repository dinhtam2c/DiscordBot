using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Pause;

[InteractionHandler("pause")]
internal sealed class PauseHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var result = await players.ExecuteAsync(guildId, new PausePlayback(), ct);
        var response = result.Before is null || !result.Before.HasCurrentTrack
            ? "There is no track currently playing."
            : result.Before.IsPaused ? "Playback is already paused." : "Playback paused.";
        await context.Responder.RespondAsync(InteractionResponse.Text(response), ct);
    }
}
