using Discord;
using Modules.Music.Tracks;

namespace Modules.Music.Features;

internal static class TrackFormatting
{
    // Escape Markdown delimiters without escaping ordinary punctuation in display names.
    public static string EscapeText(string text)
        => text.Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("~", "\\~")
            .Replace("`", "\\`")
            .Replace("[", "\\[")
            .Replace("]", "\\]");

    public static string Artist(Track track)
    {
        var name = string.IsNullOrWhiteSpace(track.Artist) ? track.UploaderName : track.Artist;
        var artist = string.IsNullOrWhiteSpace(name) ? "Unknown" : EscapeText(name);
        return string.IsNullOrWhiteSpace(track.UploaderUrl) ? artist : Format.Url(artist, track.UploaderUrl);
    }

    public static string Duration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration:mm\\:ss}"
            : duration.ToString(@"mm\:ss");

    public static string Progress(TimeSpan? position, TimeSpan duration)
    {
        var elapsed = position is { } value
            ? Duration(TimeSpan.FromTicks(Math.Clamp(value.Ticks, 0,
                duration > TimeSpan.Zero ? duration.Ticks : long.MaxValue)))
            : "\u2014";
        var total = duration > TimeSpan.Zero ? Duration(duration) : "Unknown";
        return $"{elapsed} / {total}";
    }

    public static string Entry(Track track)
        => $"**{Format.Url(EscapeText(track.Title), track.SourceUrl)}** • {Artist(track)} • `{Duration(track.Duration)}`";
}
