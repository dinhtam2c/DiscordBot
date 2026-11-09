using Discord;
using Modules.Music.Tracks;

namespace Modules.Music.Features;

internal static class TrackFormatting
{
    public static string Artist(Track track)
    {
        var name = string.IsNullOrWhiteSpace(track.Artist) ? track.UploaderName : track.Artist;
        var artist = string.IsNullOrWhiteSpace(name) ? "Unknown" : Format.Sanitize(name);
        return string.IsNullOrWhiteSpace(track.UploaderUrl) ? artist : Format.Url(artist, track.UploaderUrl);
    }

    public static string Duration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration:mm\\:ss}"
            : duration.ToString(@"mm\:ss");

    public static string Entry(Track track)
        => $"{Format.Url(Format.Sanitize(track.Title), track.SourceUrl)} - {Artist(track)} - {Duration(track.Duration)}";
}
