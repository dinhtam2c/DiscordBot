using Discord;

namespace Modules.Music.Features.Playback;

internal static class EnqueueTracksResponse
{
    public static Embed Create(EnqueueTracksResult result, EnqueueTracksMode mode)
    {
        if (result.Tracks.Count == 1)
        {
            var track = result.Tracks[0];
            var position = result.StartsPlayback ? "Now Playing"
                : mode == EnqueueTracksMode.Next ? "#1" : $"#{result.QueueCount}";
            return new EmbedBuilder()
                .WithTitle("Added to queue")
                .WithDescription($"**[{track.Title}]({track.SourceUrl})**")
                .WithThumbnailUrl(track.ThumbnailUrl)
                .AddField("Artist", track.Artist, true)
                .AddField("Duration", track.Duration.TotalHours >= 1 ? track.Duration.ToString(@"hh\:mm\:ss") : track.Duration.ToString(@"mm\:ss"), true)
                .AddField("Position", position, true)
                .Build();
        }

        var totalDuration = result.Tracks.Aggregate(TimeSpan.Zero, (sum, track) => sum + track.Duration);
        return new EmbedBuilder()
            .WithTitle("Playlist added")
            .WithDescription($"Added **{result.Tracks.Count}** tracks to the queue.")
            .AddField("Total duration", totalDuration.ToString(@"hh\:mm\:ss"), true)
            .AddField("Queue size", result.QueueCount.ToString(), true)
            .Build();
    }
}
