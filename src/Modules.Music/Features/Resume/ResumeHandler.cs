using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Resume;

[InteractionHandler("resume")]
internal sealed class ResumeHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var result = await players.ExecuteAsync(guildId, new ResumePlayback(), ct);
        var response = result.Before is null || !result.Before.HasCurrentTrack
            ? "There is no track currently playing."
            : !result.Before.IsPaused ? "Playback is not paused." : "Playback resumed.";
        await context.Responder.RespondAsync(InteractionResponse.Text(response), ct);
    }
}
