using System.Text;
using Discord;
using Modules.Music.Players;

namespace Modules.Music.Features.Queue;

internal static class QueueResponse
{
    private const int PageSize = 10;

    public static MessageComponent Create(PlayerSnapshot? player, int page)
    {
        var count = player?.Queue.Count ?? 0;
        var pageCount = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        page = Math.Clamp(page, 0, pageCount - 1);
        var description = new StringBuilder();
        if (player?.CurrentTrack is { } current)
        {
            description.AppendLine($"**{(player.IsPaused ? "Paused" : "Now playing")}:** {TrackFormatting.CompactEntry(current, 300)}");
            description.AppendLine();
        }
        if (count > 0)
        {
            description.AppendLine("**Up next:**");
            var end = Math.Min((long)(page + 1) * PageSize, count);
            for (var index = page * PageSize; index < end; index++)
                description.AppendLine($"{index + 1}. {TrackFormatting.CompactEntry(player!.Queue[index].Track, 300)}");
        }
        else
        {
            description.AppendLine(player?.HasCurrentTrack == true ? "No songs are queued." : "The queue is empty.");
        }

        var container = new ContainerBuilder().WithTextDisplay("### Queue\n" + description.ToString().TrimEnd())
            .WithTextDisplay($"-# Page {page + 1} of {pageCount} \u00B7 {count} {(count == 1 ? "song" : "songs")} queued");
        if (pageCount > 1)
            container.WithActionRow(new ActionRowBuilder()
                .WithButton("Previous", $"music:queue:{page - 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25C0\uFE0F"), disabled: page == 0)
                .WithButton("Next", $"music:queue:{page + 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25B6\uFE0F"), disabled: page == pageCount - 1));
        return MusicCard.Build(container);
    }
}
