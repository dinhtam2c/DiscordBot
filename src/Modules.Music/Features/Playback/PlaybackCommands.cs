using Application.Discord.Messages;
using System.Globalization;
using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;
using Domain.ValueObjects;
using Modules.Music.Players;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Playback;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class PlaybackCommands(
    EnqueueTracksUseCase enqueueTracks,
    SeekPlaybackUseCase seekPlayback,
    PlayerCoordinator players,
    IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("play", "Play a song or playlist, or add it to the queue if music is already playing.")]
    public Task PlayAsync([Summary(description: "A song name to search on YouTube, or a YouTube or SoundCloud link.")] string query)
        => EnqueueAsync(query, EnqueueTracksMode.Queue);

    [SlashCommand("playnext", "Play a song or playlist next, after the current song.")]
    public Task PlayNextAsync([Summary(description: "A song name to search on YouTube, or a YouTube or SoundCloud link.")] string query)
        => EnqueueAsync(query, EnqueueTracksMode.Next);

    private async Task EnqueueAsync(string query, EnqueueTracksMode mode)
    {
        var result = await enqueueTracks.ExecuteAsync(new EnqueueTracksInput(
            GuildId, UserId, new ChannelId(Context.Channel.Id), Context.Interaction.Id, query, mode), Ct);
        await CompleteAsync(embed: EnqueueTracksResponse.Create(result, mode));
    }

    [SlashCommand("pause", "Pause the music.")]
    public async Task PauseAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new PausePlayback(), Ct);
        await CompleteAsync(!result.HadCurrentTrack
            ? "Nothing is playing right now."
            : result.WasManuallyPaused ? "The music is already paused." : "Paused the music.");
    }

    [SlashCommand("resume", "Resume the music.")]
    public async Task ResumeAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new ResumePlayback(), Ct);
        await CompleteAsync(ResumeResponse(result));
    }

    private static string ResumeResponse(PlayerResult result)
        => !result.HadCurrentTrack ? "Nothing is playing right now."
            : result.IsPausedForEmptyChannel ? "Playback will resume when someone joins the voice channel."
            : !result.WasPaused ? "The music isn't paused." : "Resumed the music.";

    [SlashCommand("skip", "Skip the current song.")]
    public async Task SkipAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new SkipTrack(), Ct);
        await CompleteAsync(result.PreviousItem?.Track is { } skipped
            ? $"Skipped **{MessageFormatting.Link(skipped.Title, skipped.SourceUrl)}**."
            : "Nothing is playing right now.");
    }

    [SlashCommand("seek", "Jump to a position in the current song.")]
    public async Task SeekAsync([Summary(description: "Position in seconds, mm:ss, or hh:mm:ss, for example 90 or 1:30.")] string position)
    {
        var target = SeekPlaybackUseCase.ParsePosition(position);
        await seekPlayback.ExecuteAsync(GuildId, target, Ct);
        await CompleteAsync($"Jumped to **{TrackFormatting.Duration(target)}**.");
    }

    [SlashCommand("stop", "Stop the music and clear the queue.")]
    public async Task StopAsync()
    {
        await players.ExecuteAsync(GuildId, new StopPlayback(), Ct);
        await CompleteAsync("Stopped the music and cleared the queue.");
    }

    [ComponentInteraction("music:pause:*")]
    public Task PauseButtonAsync(string itemId) => ControlAsync(itemId, new PausePlayback());

    [ComponentInteraction("music:resume:*")]
    public async Task ResumeButtonAsync(string itemId)
    {
        var result = await ControlAsync(itemId, new ResumePlayback());
        await FollowupAsync(ResumeResponse(result), ephemeral: true, allowedMentions: AllowedMentions.None,
            options: new RequestOptions { CancelToken = Ct });
    }

    [ComponentInteraction("music:skip:*")]
    public Task SkipButtonAsync(string itemId) => ControlAsync(itemId, new SkipTrack());

    [ComponentInteraction("music:stop:*")]
    public Task StopButtonAsync(string itemId) => ControlAsync(itemId, new StopPlayback());

    private Task<PlayerResult> ControlAsync(string itemId, PlayerRequest request)
    {
        if (!Guid.TryParseExact(itemId, "N", out var id))
            throw new UserFriendlyException("These controls are no longer available. Use `/nowplaying` to see what's playing.");

        return players.ExecuteAsync(GuildId, request, Ct, expectedQueueItemId: id);
    }

    [SlashCommand("volume", "Set the volume from 0 to 100%.")]
    public async Task VolumeAsync([Summary(description: "Volume from 0 (muted) to 100%.")] long level)
    {
        if (level is < 0 or > 100)
            throw new UserFriendlyException("Volume must be between 0 and 100.");
        var volume = Volume.Create((int)level);
        await players.ExecuteAsync(GuildId, new SetVolume(volume), Ct);
        await CompleteAsync($"Volume set to **{volume.Value}%**.");
    }

    [SlashCommand("loop", "Repeat the current song or the whole queue.")]
    public async Task LoopAsync(
        [Summary(description: "What to repeat, or Off to stop repeating.")]
        [Choice("Off", "None"), Choice("Current song", "Track"), Choice("Whole queue", "Queue")] string mode)
    {
        if (!Enum.TryParse<LoopMode>(mode, out var loopMode) || !Enum.IsDefined(loopMode))
            throw new UserFriendlyException("Choose Off, Current song, or Whole queue.");
        await players.ExecuteAsync(GuildId, new SetLoopMode(loopMode), Ct);
        await CompleteAsync(loopMode switch
        {
            LoopMode.Track => "Repeating the current song.",
            LoopMode.Queue => "Repeating the queue.",
            _ => "Repeat is off.",
        });
    }

    [SlashCommand("nowplaying", "See what's playing, its progress, and who requested it.")]
    public async Task NowPlayingAsync()
    {
        var player = await players.GetSnapshotAsync(GuildId, Ct)
            ?? throw new UserFriendlyException("Use `/play` to start playing music first.");
        if (player.CurrentQueueItem is not { } item)
        {
            await CompleteAsync("Nothing is playing right now.");
            return;
        }

        var requester = Context.Guild.GetUser(item.RequesterId.Value);
        var requesterName = requester?.DisplayName ?? $"User {item.RequesterId}";
        var track = item.Track;
        await CompleteAsync(embed: new EmbedBuilder()
            .WithTitle(player.IsPaused ? "Paused" : "Now playing")
            .WithDescription($"**{MessageFormatting.Link(track.Title, track.SourceUrl)}**")
            .WithThumbnailUrl(track.ThumbnailUrl)
            .AddField("Artist", TrackFormatting.Artists(track), true)
            .AddField("Requested by", MessageFormatting.Text(requesterName), true)
            .AddField("Requested at", item.RequestedAt.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture), true)
            .AddField("Progress", TrackFormatting.ProgressBar(player.PlaybackPosition, track.Duration))
            .Build());
    }
}
