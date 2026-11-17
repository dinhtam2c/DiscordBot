using Discord;
using Modules.Music.Players;

namespace Modules.Music.Features.History;

internal static class HistoryResponse
{
    private const int PageSize = 10;

    public static MessageComponent Create(IReadOnlyList<PlaybackHistoryEntry> entries,
        int page, int capacity)
    {
        var pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        var visible = entries.Skip(page * PageSize).Take(PageSize).ToArray();
        var description = visible.Length == 0 ? "No songs have been played yet."
            : string.Join("\n\n", visible.Select((entry, index) =>
                $"{page * PageSize + index + 1}. {TrackFormatting.CompactEntry(entry.Item.Track, 280)}\n{Details(entry)}"));
        var container = new ContainerBuilder().WithTextDisplay("### Playback history\n" + description)
            .WithTextDisplay($"-# Newest first \u00B7 Page {page + 1} of {pages} \u00B7 {entries.Count}/{capacity} plays");
        if (pages > 1)
            container.WithActionRow(new ActionRowBuilder()
                .WithButton("Previous", $"music:history-page:{page - 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25C0\uFE0F"), disabled: page == 0)
                .WithButton("Next", $"music:history-page:{page + 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25B6\uFE0F"), disabled: page == pages - 1));
        return MusicCard.Build(container);
    }

    private static string Details(PlaybackHistoryEntry entry)
    {
        var requester = entry.RequesterId is { } user ? $"Requested by <@{user.Value}>" : "Added by Autoplay";
        var skipped = entry.SkippedBy is { } skipper ? $" \u00B7 Skipped by <@{skipper.Value}>" : string.Empty;
        return $"{Status(entry.Status)} \u00B7 {requester}{skipped}";
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
