using System.Globalization;
using Application.Abstractions;
using Application.Discord.Interactions;
using Discord;
using Discord.Interactions;
using Modules.Music.Players;
using Modules.Music.Players.Coordination;

namespace Modules.Music.Features.Playback;

[CommandContextType(InteractionContextType.Guild)]
internal sealed class PlaybackCommands(
    EnqueueTracksUseCase enqueueTracks,
    PlayerCoordinator players,
    IAppLifetime lifetime) : DiscordCommands(lifetime)
{
    [SlashCommand("play", "Add a track or playlist to the queue and start playback if idle.")]
    public Task PlayAsync([Summary(description: "Search YouTube, or paste a YouTube or SoundCloud track or playlist URL.")] string query)
        => EnqueueAsync(query, EnqueueTracksMode.Queue);

    [SlashCommand("playnext", "Add a track or playlist to the front of the queue without interrupting the current track.")]
    public Task PlayNextAsync([Summary(description: "Search YouTube, or paste a YouTube or SoundCloud track or playlist URL.")] string query)
        => EnqueueAsync(query, EnqueueTracksMode.Next);

    private async Task EnqueueAsync(string query, EnqueueTracksMode mode)
    {
        var result = await enqueueTracks.ExecuteAsync(new EnqueueTracksInput(GuildId, UserId, query, mode), Ct);
        await CompleteAsync(embed: EnqueueTracksResponse.Create(result, mode));
    }

    [SlashCommand("pause", "Pause the current track.")]
    public async Task PauseAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new PausePlayback(), Ct);
        await CompleteAsync(!result.HadCurrentTrack
            ? "No track is currently playing."
            : result.WasPaused ? "Playback is already paused." : "Playback paused.");
    }

    [SlashCommand("resume", "Resume playback of the paused track.")]
    public async Task ResumeAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new ResumePlayback(), Ct);
        await CompleteAsync(!result.HadCurrentTrack
            ? "No track is currently playing."
            : !result.WasPaused ? "Playback is not paused." : "Playback resumed.");
    }

    [SlashCommand("skip", "Skip the current track.")]
    public async Task SkipAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new SkipTrack(), Ct);
        await CompleteAsync(result.PreviousItem?.Track is { } skipped
            ? $"Skipped **{Format.Url(Format.Sanitize(skipped.Title), skipped.SourceUrl)}**."
            : "No track is currently playing.");
    }

    [SlashCommand("stop", "Stop playback and clear the queue while staying in the voice channel.")]
    public async Task StopAsync()
    {
        await players.ExecuteAsync(GuildId, new StopPlayback(), Ct);
        await CompleteAsync("Playback stopped and queue cleared.");
    }

    [SlashCommand("volume", "Set the playback volume from 0 to 100%.")]
    public async Task VolumeAsync([Summary(description: "Volume percentage, from 0 (muted) to 100.")] long level)
    {
        if (level is < 0 or > 100)
            throw new UserFriendlyException("Volume must be between 0 and 100.");
        var volume = Volume.Create((int)level);
        await players.ExecuteAsync(GuildId, new SetVolume(volume), Ct);
        await CompleteAsync($"Volume set to **{volume.Value}%**.");
    }

    [SlashCommand("loop", "Repeat the current track or the queue, or turn looping off.")]
    public async Task LoopAsync(
        [Summary(description: "Repeat the current track, repeat the queue, or turn looping off.")]
        [Choice("Off", "None"), Choice("Current track", "Track"), Choice("Queue", "Queue")] string mode)
    {
        if (!Enum.TryParse<LoopMode>(mode, out var loopMode) || !Enum.IsDefined(loopMode))
            throw new UserFriendlyException("Choose a loop mode: Off, Current track, or Queue.");
        await players.ExecuteAsync(GuildId, new SetLoopMode(loopMode), Ct);
        await CompleteAsync(loopMode switch
        {
            LoopMode.Track => "Now looping the current track.",
            LoopMode.Queue => "Now looping the queue.",
            _ => "Looping is off.",
        });
    }

    [SlashCommand("nowplaying", "Show the current track and who requested it.")]
    public async Task NowPlayingAsync()
    {
        var player = await players.GetSnapshotAsync(GuildId, Ct)
            ?? throw new UserFriendlyException("No player is active. Add a track with `/play` first.");
        if (player.CurrentQueueItem is not { } item)
        {
            await CompleteAsync("No track is currently playing.");
            return;
        }

        var requester = Context.Guild.GetUser(item.RequesterId.Value);
        var requesterName = requester?.DisplayName ?? $"User {item.RequesterId}";
        var track = item.Track;
        await CompleteAsync(embed: new EmbedBuilder()
            .WithTitle(player.IsPaused ? "Playback paused" : "Now playing")
            .WithDescription($"**{Format.Url(Format.Sanitize(track.Title), track.SourceUrl)}**")
            .WithThumbnailUrl(track.ThumbnailUrl)
            .AddField("Artist", string.IsNullOrWhiteSpace(track.Artist) ? "Unknown artist" : Format.Sanitize(track.Artist), true)
            .AddField("Duration", track.Duration.TotalHours >= 1 ? track.Duration.ToString(@"hh\:mm\:ss") : track.Duration.ToString(@"mm\:ss"), true)
            .AddField("Requested by", Format.Sanitize(requesterName), true)
            .AddField("Requested at", item.RequestedAt.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture), true)
            .Build());
    }
}
