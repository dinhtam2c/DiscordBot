using Discord;
using Modules.Music.Players;

namespace Modules.Music.Features.Playback;

internal static class PlaybackMessage
{
    public static MessageComponent Create(PlayerSnapshot player, TimeSpan? position, string? title = null,
        bool controls = false, bool requestedAt = false)
    {
        var item = player.CurrentQueueItem!;
        title ??= !player.IsPaused ? "Now playing"
            : player.IsPausedForEmptyChannel ? "Paused \u00B7 Voice channel is empty" : "Paused";
        var requester = item.RequesterId is { } user ? $"Requested by: <@{user.Value}>" : "Added by: Autoplay";
        var card = MusicCard.Create(title, item.Track, requester)
            .WithSeparator(new SeparatorBuilder())
            .WithTextDisplay(TrackFormatting.ProgressBar(position, item.Track.Duration));
        var footer = $"-# Volume {player.Volume.Value}% \u00B7 {player.Queue.Count} {(player.Queue.Count == 1 ? "song" : "songs")} queued";
        if (requestedAt)
            footer += $" \u00B7 {(item.IsAutoplay ? "Added" : "Requested")} <t:{item.RequestedAt.ToUnixTimeSeconds()}:t>";
        card.WithTextDisplay(footer);
        if (controls)
        {
            var id = item.Id.ToString("N");
            var repeat = player.LoopMode switch { LoopMode.Track => "Song", LoopMode.Queue => "Queue", _ => "Off" };
            card.WithActionRow(new ActionRowBuilder()
                .WithButton(player.IsPaused ? "Resume" : "Pause", $"music:{(player.IsPaused ? "resume" : "pause")}:{id}",
                    player.IsPaused ? ButtonStyle.Primary : ButtonStyle.Secondary,
                    emote: new Emoji(player.IsPaused ? "\u25B6\uFE0F" : "\u23F8\uFE0F"))
                .WithButton("Skip", $"music:skip:{id}", ButtonStyle.Secondary, emote: new Emoji("\u23ED\uFE0F"))
                .WithButton("Queue", "music:queue-open", ButtonStyle.Secondary, emote: new Emoji("\uD83D\uDCCB"))
                .WithButton("Stop", $"music:stop:{id}", ButtonStyle.Danger, emote: new Emoji("\u23F9\uFE0F")))
                .WithActionRow(new ActionRowBuilder()
                    .WithButton($"Autoplay: {(player.AutoplayEnabled ? "On" : "Off")}", $"music:autoplay:{id}",
                        player.AutoplayEnabled ? ButtonStyle.Success : ButtonStyle.Secondary, emote: new Emoji("\u267E\uFE0F"))
                    .WithButton($"Repeat: {repeat}", $"music:repeat:{id}",
                        player.LoopMode == LoopMode.None ? ButtonStyle.Secondary : ButtonStyle.Success,
                        emote: new Emoji("\uD83D\uDD01")));
        }
        return MusicCard.Build(card);
    }
}
