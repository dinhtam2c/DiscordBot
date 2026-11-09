using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Stop;

[InteractionHandler("stop")]
internal sealed class StopHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        await players.ExecuteAsync(guildId, new StopPlayback(), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text("Playback stopped."), ct);
    }
}
