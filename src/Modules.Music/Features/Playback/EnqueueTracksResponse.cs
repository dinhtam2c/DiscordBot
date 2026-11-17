using Application.Discord.Messages;
using Discord;

namespace Modules.Music.Features.Playback;

internal static class EnqueueTracksResponse
{
    public static MessageComponent Create(EnqueueTracksResult result, EnqueueTracksMode mode)
    {
        if (result.Playlist is null && result.Tracks.Count == 1)
        {
            var track = result.Tracks[0];
            var position = result.StartsPlayback ? "Playing now" : mode == EnqueueTracksMode.Next ? "Queue #1" : $"Queue #{result.QueueCount}";
            return MusicCard.Build(MusicCard.Create("Added to queue", track)
                .WithTextDisplay($"-# {TrackFormatting.Duration(track.Duration)} \u00B7 {position}"));
        }
        var count = result.Tracks.Count;
        var description = $"Added **{count} {(count == 1 ? "song" : "songs")}** to the queue.";
        if (result.Playlist is { } playlist)
        {
            var playlistTitle = $"Playlist: **{MessageFormatting.Link(playlist.Title, playlist.SourceUrl)}**";
            description = count <= 10
                ? playlistTitle + "\n\n" + string.Join('\n', result.Tracks.Select((track, index) => $"{index + 1}. {TrackFormatting.CompactEntry(track, 280)}"))
                : playlistTitle + "\n" + description;
            var creator = string.IsNullOrWhiteSpace(playlist.CreatorName) ? "Unknown" : playlist.CreatorName;
            description += "\n\nCreated by: " + MessageFormatting.Link(creator, playlist.CreatorUrl);
        }
        var duration = result.Tracks.Aggregate(TimeSpan.Zero, (sum, track) => sum + track.Duration);
        return MusicCard.Build(new ContainerBuilder().WithTextDisplay("### Added to queue\n" + description)
            .WithTextDisplay($"-# Total duration: {TrackFormatting.Duration(duration)}"));
    }
}
