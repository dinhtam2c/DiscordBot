using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Leave;

[InteractionHandler("leave")]
internal sealed class LeaveHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        await players.ExecuteAsync(guildId, new LeaveVoice(), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text("Left."), ct);
    }
}
