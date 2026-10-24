using Application.Discord.Interactions;
using Application.Discord.Interactions.Context;
using Application.Discord.Interactions.Handler;
using Application.Discord.Interactions.Response;
using Modules.Music.Application.UseCases.EnqueueTracks;

namespace Modules.Music.Application.Shortcuts;

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

        if (result.Tracks.Count == 1)
        {
            var track = result.Tracks[0];
            var position = result.StartsPlayback
                ? "Now Playing"
                : shortcut.Mode == EnqueueTracksMode.Next ? "#1" : $"#{result.QueueCount}";

            await context.Responder.RespondAsync(
                new InteractionResponse
                {
                    Embeds =
                    [
                        new ResponseEmbed
                        {
                            Title = "Added to queue",
                            Description = $"**[{track.Title}]({track.SourceUrl})**",
                            ThumbnailUrl = track.ThumbnailUrl,
                            Fields =
                            [
                                new("Artist", track.Artist, true),
                                new("Duration", track.Duration.TotalHours >= 1 ? track.Duration.ToString(@"hh\:mm\:ss") : track.Duration.ToString(@"mm\:ss"), true),
                                new("Position", position, true),
                            ]
                        }
                    ]
                },
                ct);
            return;
        }

        var totalDuration = result.Tracks.Aggregate(TimeSpan.Zero, (sum, track) => sum + track.Duration);
        await context.Responder.RespondAsync(
            new InteractionResponse
            {
                Embeds =
                [
                    new ResponseEmbed
                    {
                        Title = "Playlist added",
                        Description = $"Added **{result.Tracks.Count}** tracks to the queue.",
                        Fields =
                        [
                            new("Total duration", totalDuration.ToString(@"hh\:mm\:ss"), true),
                            new("Queue size", result.QueueCount.ToString(), true),
                        ]
                    }
                ]
            },
            ct);
    }
}
