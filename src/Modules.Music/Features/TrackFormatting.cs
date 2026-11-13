using Application.Discord.Messages;
using Modules.Music.Tracks;

namespace Modules.Music.Features;

internal static class TrackFormatting
{
    public static string Artist(Track track)
    {
        var name = string.IsNullOrWhiteSpace(track.Artist) ? track.UploaderName : track.Artist;
        var artist = string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
        return string.IsNullOrWhiteSpace(track.UploaderUrl) ? MessageFormatting.Text(artist) : MessageFormatting.Link(artist, track.UploaderUrl);
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

    public static string ProgressBar(TimeSpan? position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return position is { } elapsed ? Duration(elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed) : "\u2014";
        if (position is null)
            return Progress(null, duration);

        const int width = 16;
        var ratio = Math.Clamp(position.Value.Ticks / (double)duration.Ticks, 0, 1);
        var marker = (int)(ratio * (width - 1));
        return $"{new string('\u2501', marker)}\u25CF{new string('\u2500', width - marker - 1)}\n{Progress(position, duration)}";
    }

    public static string Entry(Track track)
        => $"**{MessageFormatting.Link(track.Title, track.SourceUrl)}** • {Artist(track)} • `{Duration(track.Duration)}`";
}
