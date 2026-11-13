using Application.Discord.Messages;
using System.Text;
using Discord;
using Modules.Music.Players;
using Modules.Music.Tracks;

namespace Modules.Music.Features.Queue;

internal static class QueueResponse
{
    private const int PageSize = 10;

    public static (Embed Embed, MessageComponent Components) Create(PlayerSnapshot? player, int page)
    {
        var count = player?.Queue.Count ?? 0;
        var pageCount = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        page = Math.Clamp(page, 0, pageCount - 1);
        var description = new StringBuilder();
        if (player?.CurrentTrack is { } current)
        {
            description.AppendLine($"**{(player.IsPaused ? "Paused" : "Now playing")}:** {Entry(current)}");
            description.AppendLine();
        }
        if (count > 0)
        {
            description.AppendLine("**Up next:**");
            var end = Math.Min((long)(page + 1) * PageSize, count);
            for (var index = page * PageSize; index < end; index++)
                description.AppendLine($"{index + 1}. {Entry(player!.Queue[index].Track)}");
        }
        else
        {
            description.AppendLine(player?.HasCurrentTrack == true ? "No songs are queued." : "The queue is empty.");
        }

        var components = new ComponentBuilder();
        if (pageCount > 1)
        {
            components
                .WithButton("Previous", $"music:queue:{page - 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25C0\uFE0F"), disabled: page == 0)
                .WithButton("Next", $"music:queue:{page + 1}", ButtonStyle.Secondary,
                    emote: new Emoji("\u25B6\uFE0F"), disabled: page == pageCount - 1);
        }

        return (new EmbedBuilder()
            .WithTitle("Queue")
            .WithDescription(description.ToString().TrimEnd())
            .WithFooter($"Page {page + 1} of {pageCount} • {count} {(count == 1 ? "song" : "songs")} queued")
            .Build(), components.Build());
    }

    private static string Entry(Track track)
    {
        var entry = TrackFormatting.Entry(track);
        if (entry.Length <= 340)
            return entry;

        // Keep ten entries inside Discord's description limit even with long source metadata.
        var compact = track with
        {
            Title = Shorten(track.Title, 80),
            Artist = Shorten(track.Artist, 40),
            UploaderName = track.UploaderName is { } name ? Shorten(name, 40) : null,
        };
        entry = TrackFormatting.Entry(compact);
        if (entry.Length <= 340)
            return entry;

        var artist = string.IsNullOrWhiteSpace(compact.Artist) ? compact.UploaderName ?? "Unknown" : compact.Artist;
        return $"**{MessageFormatting.Text(compact.Title)}** • {MessageFormatting.Text(artist)} • `{TrackFormatting.Duration(compact.Duration)}`";
    }

    private static string Shorten(string text, int length)
        => text.Length <= length ? text : text[..length] + "…";
}
