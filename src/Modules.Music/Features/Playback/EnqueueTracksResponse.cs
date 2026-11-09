using Discord;

namespace Modules.Music.Features.Playback;

internal static class EnqueueTracksResponse
{
    public static Embed Create(EnqueueTracksResult result, EnqueueTracksMode mode)
    {
        if (result.Playlist is null && result.Tracks.Count == 1)
        {
            var track = result.Tracks[0];
            var position = result.StartsPlayback ? "Current track"
                : mode == EnqueueTracksMode.Next ? "#1" : $"#{result.QueueCount}";
            var trackEmbed = new EmbedBuilder()
                .WithTitle("Track added")
                .WithDescription($"**{Format.Url(Format.Sanitize(track.Title), track.SourceUrl)}**")
                .WithThumbnailUrl(track.ThumbnailUrl)
                .AddField("Artist", string.IsNullOrWhiteSpace(track.Artist) ? "Unknown artist" : Format.Sanitize(track.Artist), true);
            if (!string.IsNullOrWhiteSpace(track.UploaderName))
            {
                var uploader = Format.Sanitize(track.UploaderName);
                trackEmbed.AddField("Uploaded by", string.IsNullOrWhiteSpace(track.UploaderUrl)
                    ? uploader : Format.Url(uploader, track.UploaderUrl), true);
            }
            return trackEmbed
                .AddField("Duration", track.Duration.TotalHours >= 1 ? track.Duration.ToString(@"hh\:mm\:ss") : track.Duration.ToString(@"mm\:ss"), true)
                .AddField("Queue position", position, true)
                .Build();
        }

        var totalDuration = result.Tracks.Aggregate(TimeSpan.Zero, (sum, track) => sum + track.Duration);
        var description = $"Added **{result.Tracks.Count} {(result.Tracks.Count == 1 ? "track" : "tracks")}**.";
        var builder = new EmbedBuilder()
            .WithTitle("Playlist added")
            .WithDescription(description);
        if (result.Playlist is { } playlist)
        {
            builder.WithDescription($"**{Format.Url(Format.Sanitize(playlist.Title), playlist.SourceUrl)}**\n{description}");
            var creator = string.IsNullOrWhiteSpace(playlist.CreatorName) ? "Unknown creator" : Format.Sanitize(playlist.CreatorName);
            builder.AddField("Created by", string.IsNullOrWhiteSpace(playlist.CreatorUrl)
                ? creator : Format.Url(creator, playlist.CreatorUrl), true);
        }
        return builder
            .AddField("Total duration", totalDuration.ToString(@"hh\:mm\:ss"), true)
            .AddField("Tracks waiting", result.QueueCount.ToString(), true)
            .Build();
    }
}
