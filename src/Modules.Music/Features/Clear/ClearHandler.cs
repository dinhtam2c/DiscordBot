using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Clear;

[InteractionHandler("clear")]
internal sealed class ClearHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        await players.ExecuteAsync(guildId, new ClearQueue(), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text("Queue cleared."), ct);
    }
}
