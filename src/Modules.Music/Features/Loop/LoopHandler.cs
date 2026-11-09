using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;
using Modules.Music.Players;

namespace Modules.Music.Features.Loop;

[InteractionHandler("loop")]
internal sealed class LoopHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var mode = context.Options.GetRequired<LoopMode>("mode");
        await players.ExecuteAsync(guildId, new SetLoopMode(mode), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text($"Loop mode set to **{mode}**."), ct);
    }
}
