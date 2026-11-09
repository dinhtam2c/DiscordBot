using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Move;

[InteractionHandler("move")]
internal sealed class MoveHandler(PlayerCoordinator players) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("Guild ID is required.");
        var index = context.Options.GetRequired<long>("index");
        var newIndex = context.Options.GetRequired<long>("new_index");
        await players.ExecuteAsync(guildId, new MoveTrack(index, newIndex), ct);
        await context.Responder.RespondAsync(InteractionResponse.Text($"Moved track from position {index} to {newIndex} in the queue."), ct);
    }
}
