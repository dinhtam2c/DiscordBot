using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Modules.Music.Features.EnqueueTracks;

namespace Modules.Music.Features.PlayNext;

[InteractionHandler("playnext")]
internal sealed class PlayNextHandler(EnqueueTracksUseCase enqueueTracks) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");
        var query = context.Options.GetRequired<string>("query");
        var result = await enqueueTracks.ExecuteAsync(
            new EnqueueTracksRequest(guildId, context.UserId, query, EnqueueTracksMode.Next),
            ct);

        await context.Responder.RespondAsync(EnqueueTracksResponse.Create(result, EnqueueTracksMode.Next), ct);
    }
}
