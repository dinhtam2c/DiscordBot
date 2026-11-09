using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Modules.Music.Features.EnqueueTracks;

namespace Modules.Music.Features.Play;

[InteractionHandler("play")]
internal sealed class PlayHandler(EnqueueTracksUseCase enqueueTracks) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");
        var query = context.Options.GetRequired<string>("query");
        var result = await enqueueTracks.ExecuteAsync(
            new EnqueueTracksRequest(guildId, context.UserId, query, EnqueueTracksMode.Queue),
            ct);

        await context.Responder.RespondAsync(EnqueueTracksResponse.Create(result, EnqueueTracksMode.Queue), ct);
    }
}
