using Application.Discord.Messages;
using Modules.Music.Tracks;

namespace Modules.Music.Features;

internal static class TrackFormatting
{
    public static string Artists(Track track)
    {
        var names = track.Artists.Count > 0
            ? track.Artists : Track.SelectArtists([track.UploaderName]);
        if (names.Count == 0)
            return "Unknown";

        return string.Join(", ", names.Select(name =>
            !string.IsNullOrWhiteSpace(track.UploaderUrl)
                && string.Equals(name, track.UploaderName?.Trim(), StringComparison.OrdinalIgnoreCase)
                ? MessageFormatting.Link(name, track.UploaderUrl)
                : MessageFormatting.Text(name)));
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

        const int width = 30;
        var ratio = Math.Clamp(position.Value.Ticks / (double)duration.Ticks, 0, 1);
        var marker = (int)(ratio * (width - 1));
        return $"{new string('\u2501', marker)}\u25CF{new string('\u2500', width - marker - 1)}  {Progress(position, duration)}";
    }

    public static string Entry(Track track)
        => $"**{MessageFormatting.Link(track.Title, track.SourceUrl)}** • {Artists(track)} • `{Duration(track.Duration)}`";

    public static string CompactEntry(Track track, int maxLength = 340)
    {
        var entry = Entry(track);
        if (entry.Length <= maxLength)
            return entry;

        // Keep paginated lists inside Discord's message text limit even with long source metadata.
        var compact = track with
        {
            Title = Shorten(track.Title, 80),
        };
        entry = Entry(compact);
        if (entry.Length <= maxLength)
            return entry;

        var artist = Shorten(track.Artists.Count > 0
            ? string.Join(", ", track.Artists) : track.UploaderName ?? "Unknown", 40);
        return $"**{MessageFormatting.Text(compact.Title)}** • {MessageFormatting.Text(artist)} • `{Duration(compact.Duration)}`";
    }

    private static string Shorten(string text, int length)
        => text.Length <= length ? text : text[..length] + "…";
}
