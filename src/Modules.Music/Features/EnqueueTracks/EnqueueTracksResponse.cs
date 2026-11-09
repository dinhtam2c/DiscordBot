using Application.Discord.Interactions.Response;

namespace Modules.Music.Features.EnqueueTracks;

internal static class EnqueueTracksResponse
{
    public static InteractionResponse Create(EnqueueTracksResult result, EnqueueTracksMode mode)
    {
        if (result.Tracks.Count == 1)
        {
            var track = result.Tracks[0];
            var position = result.StartsPlayback ? "Now Playing"
                : mode == EnqueueTracksMode.Next ? "#1" : $"#{result.QueueCount}";

            return new InteractionResponse
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
            };
        }

        var totalDuration = result.Tracks.Aggregate(TimeSpan.Zero, (sum, track) => sum + track.Duration);
        return new InteractionResponse
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
        };
    }
}
