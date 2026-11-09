using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Remove;

[InteractionHandler("remove")]
internal sealed class RemoveHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var index = context.Options.GetRequired<long>("index");
        await players.ExecuteAsync(guildId, new RemoveTrack(index), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text($"Removed track at position {index} from the queue."), ct);
    }
}
