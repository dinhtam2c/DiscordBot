using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Modules.Music.Features.EnqueueTracks;

namespace Modules.Music.Features.Shortcuts;

internal sealed class ShortcutInteractionHandler(
    MusicShortcutCatalog catalog,
    EnqueueTracksUseCase enqueueTracks) : IInteractionHandler
{
    public async Task HandleAsync(InteractionContext context, CancellationToken ct)
    {
        var guildId = context.GuildId ?? throw new InvalidOperationException("GuildId is not found.");
        if (!catalog.TryGet(context.CommandName, out var shortcut) || shortcut is null)
            throw new UserFriendlyException($"The shortcut /{context.CommandName} is no longer configured.");

        var result = await enqueueTracks.ExecuteAsync(
            new EnqueueTracksRequest(guildId, context.UserId, shortcut.Query, shortcut.Mode),
            ct);

        await context.Responder.RespondAsync(EnqueueTracksResponse.Create(result, shortcut.Mode), ct);
    }
}
