using System.Globalization;
using Discord;
using Modules.Music.Players;
using Modules.Music.Tracks;

namespace Modules.Music.Features.History;

internal static class HistoryResponse
{
    private const int PageSize = 10;

    public static (Embed Embed, MessageComponent Components) Create(IReadOnlyList<PlaybackHistoryEntry> entries,
        int page, Func<IReadOnlyList<Track>, Guid> createSelection, int capacity)
    {
        var pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        var visible = entries.Skip(page * PageSize).Take(PageSize).ToArray();
        var components = new ComponentBuilder();
        if (visible.Length > 0)
        {
            var id = createSelection(visible.Select(entry => entry.Item.Track).ToArray());
            var menu = new SelectMenuBuilder().WithCustomId($"music:history-select:{id:N}")
                .WithPlaceholder("Choose a song to add to the queue").WithMinValues(1).WithMaxValues(1);
            for (var index = 0; index < visible.Length; index++)
            {
                var entry = visible[index];
                var title = entry.Item.Track.Title;
                if (title.Length > 90) title = title[..89] + "\u2026";
                menu.AddOption($"{page * PageSize + index + 1}. {title}", index.ToString(CultureInfo.InvariantCulture),
                    $"{Status(entry.Status)} - {TrackFormatting.Duration(entry.Item.Track.Duration)}");
            }
            components.WithSelectMenu(menu, row: 0);
        }
        if (pages > 1)
            components.WithButton("Previous", $"music:history-page:{page - 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25C0\uFE0F"), disabled: page == 0, row: 1)
                .WithButton("Next", $"music:history-page:{page + 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25B6\uFE0F"), disabled: page == pages - 1, row: 1);
        var description = visible.Length == 0 ? "No songs have been played yet."
            : string.Join("\n\n", visible.Select((entry, index) =>
                $"{page * PageSize + index + 1}. {TrackFormatting.CompactEntry(entry.Item.Track)}\n{Status(entry.Status)}"));
        return (new EmbedBuilder().WithTitle("Playback history").WithDescription(description)
            .WithFooter($"Newest first \u00B7 Page {page + 1} of {pages} \u00B7 {entries.Count}/{capacity} plays")
            .Build(), components.Build());
    }

    private static string Status(PlaybackHistoryStatus status) => status switch
    {
        PlaybackHistoryStatus.Playing => "Playing now",
        PlaybackHistoryStatus.Completed => "Played",
        PlaybackHistoryStatus.Skipped => "Skipped",
        PlaybackHistoryStatus.Failed => "Failed",
        _ => "Stopped",
    };
}
