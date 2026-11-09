using Discord;

namespace Modules.Music.Features.Playback;

internal static class EnqueueTracksResponse
{
    public static Embed Create(EnqueueTracksResult result, EnqueueTracksMode mode)
    {
        if (result.Playlist is null && result.Tracks.Count == 1)
        {
            var track = result.Tracks[0];
            var position = result.StartsPlayback ? "Playing now"
                : mode == EnqueueTracksMode.Next ? "#1" : $"#{result.QueueCount}";
            var trackEmbed = new EmbedBuilder()
                .WithTitle("Added to queue")
                .WithDescription($"**{Format.Url(TrackFormatting.EscapeText(track.Title), track.SourceUrl)}**")
                .WithThumbnailUrl(track.ThumbnailUrl)
                .AddField("Artist", TrackFormatting.Artist(track), true);
            return trackEmbed
                .AddField("Duration", TrackFormatting.Duration(track.Duration), true)
                .AddField("Position", position, true)
                .Build();
        }

        var totalDuration = result.Tracks.Aggregate(TimeSpan.Zero, (sum, track) => sum + track.Duration);
        var description = $"Added **{result.Tracks.Count} {(result.Tracks.Count == 1 ? "song" : "songs")}** to the queue.";
        var builder = new EmbedBuilder()
            .WithTitle("Added to queue")
            .WithDescription(description);
        if (result.Playlist is { } playlist)
        {
            var playlistTitle = $"Playlist: **{Format.Url(TrackFormatting.EscapeText(playlist.Title), playlist.SourceUrl)}**";
            if (result.Tracks.Count <= 10)
            {
                description = playlistTitle + "\n\n" + string.Join('\n', result.Tracks.Select(
                    (track, index) => $"{index + 1}. {TrackFormatting.Entry(track)}"));
            }
            else
            {
                description = $"{playlistTitle}\n{description}";
            }
            builder.WithDescription(description);
            var creator = string.IsNullOrWhiteSpace(playlist.CreatorName) ? "Unknown" : TrackFormatting.EscapeText(playlist.CreatorName);
            builder.AddField("Created by", string.IsNullOrWhiteSpace(playlist.CreatorUrl)
                ? creator : Format.Url(creator, playlist.CreatorUrl), true);
        }
        return builder
            .AddField("Total duration", TrackFormatting.Duration(totalDuration), true)
            .Build();
    }
}
