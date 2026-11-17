using System.Globalization;
using Application.Discord.Messages;
using Discord;
using Modules.Music.Tracks;

namespace Modules.Music.Features.Search;

internal static class SearchResponse
{
    public static MessageComponent Create(IReadOnlyList<Track> tracks, Guid id, TrackSource source)
    {
        var menu = new SelectMenuBuilder()
            .WithCustomId($"music:search:{id:N}")
            .WithPlaceholder("Choose a song to add to the queue")
            .WithMinValues(1).WithMaxValues(1);
        var entries = new List<string>();
        for (var index = 0; index < tracks.Count; index++)
        {
            var track = tracks[index];
            var title = Shorten(track.Title, 100);
            var artist = Shorten(track.Artists.Count > 0 ? string.Join(", ", track.Artists) : track.UploaderName ?? "Unknown", 70);
            menu.AddOption($"{index + 1}. {Shorten(title, 96)}", index.ToString(CultureInfo.InvariantCulture),
                $"{artist} \u2022 {TrackFormatting.Duration(track.Duration)}");
            // Bound metadata and URL lengths so five entries stay within Discord's message text limit.
            var label = track.SourceUrl.Length <= 300
                ? MessageFormatting.Link(title, track.SourceUrl) : MessageFormatting.Text(title);
            entries.Add($"{index + 1}. **{label}** \u2022 {MessageFormatting.Text(artist)} \u2022 `{TrackFormatting.Duration(track.Duration)}`");
        }
        var container = new ContainerBuilder()
            .WithTextDisplay($"### {(source == TrackSource.SoundCloud ? "SoundCloud search" : "YouTube search")}\n" + string.Join("\n\n", entries))
            .WithTextDisplay("-# Choose one song below \u00B7 Results expire in 5 minutes")
            .WithActionRow(new ActionRowBuilder().WithSelectMenu(menu));
        return MusicCard.Build(container);
    }

    private static string Shorten(string text, int length)
        => text.Length <= length ? text : text[..(length - 1)] + "\u2026";
}
