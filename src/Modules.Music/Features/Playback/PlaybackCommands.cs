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
    [SlashCommand("play", "Play a song in the voice channel.")]
    public Task PlayAsync([Summary(description: "The song to play.")] string query)
        => EnqueueAsync(query, EnqueueTracksMode.Queue);

    [SlashCommand("playnext", "Play a song next in the queue.")]
    public Task PlayNextAsync([Summary(description: "The song to play.")] string query)
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
            ? "There is no track currently playing."
            : result.WasPaused ? "Playback is already paused." : "Playback paused.");
    }

    [SlashCommand("resume", "Resume the paused track.")]
    public async Task ResumeAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new ResumePlayback(), Ct);
        await CompleteAsync(!result.HadCurrentTrack
            ? "There is no track currently playing."
            : !result.WasPaused ? "Playback is not paused." : "Playback resumed.");
    }

    [SlashCommand("skip", "Skip the current track.")]
    public async Task SkipAsync()
    {
        var result = await players.ExecuteAsync(GuildId, new SkipTrack(), Ct);
        await CompleteAsync(result.PreviousItem?.Track is { } skipped
            ? $"Skipped: {skipped.Title}"
            : "There is no track currently playing.");
    }

    [SlashCommand("stop", "Stop and clear the current queue.")]
    public async Task StopAsync()
    {
        await players.ExecuteAsync(GuildId, new StopPlayback(), Ct);
        await CompleteAsync("Playback stopped.");
    }

    [SlashCommand("volume", "Set the playback volume.")]
    public async Task VolumeAsync([Summary(description: "Volume from 0 to 100 percent.")] long level)
    {
        if (level is < 0 or > 100)
            throw new UserFriendlyException("Volume must be between 0 and 100.");
        var volume = Volume.Create((int)level);
        await players.ExecuteAsync(GuildId, new SetVolume(volume), Ct);
        await CompleteAsync($"Volume set to **{volume.Value}%**.");
    }

    [SlashCommand("loop", "Set the loop mode.")]
    public async Task LoopAsync(
        [Summary(description: "The loop mode to set.")]
        [Choice("Off", "None"), Choice("Track", "Track"), Choice("Queue", "Queue")] string mode)
    {
        if (!Enum.TryParse<LoopMode>(mode, out var loopMode) || !Enum.IsDefined(loopMode))
            throw new UserFriendlyException("Invalid loop mode.");
        await players.ExecuteAsync(GuildId, new SetLoopMode(loopMode), Ct);
        await CompleteAsync($"Loop mode set to **{loopMode}**.");
    }

    [SlashCommand("nowplaying", "Show the currently playing track.")]
    public async Task NowPlayingAsync()
    {
        var player = await players.GetSnapshotAsync(GuildId, Ct)
            ?? throw new UserFriendlyException("Player not found. Start playing music first.");
        if (player.CurrentQueueItem is not { } item)
        {
            await CompleteAsync("Nothing is currently playing.");
            return;
        }

        var requester = Context.Guild.GetUser(item.RequesterId.Value);
        var requesterName = requester?.DisplayName ?? $"User {item.RequesterId}";
        var track = item.Track;
        await CompleteAsync(embed: new EmbedBuilder()
            .WithTitle("Now Playing")
            .WithDescription($"**[{track.Title}]({track.SourceUrl})**")
            .WithThumbnailUrl(track.ThumbnailUrl)
            .AddField("Artist", track.Artist, true)
            .AddField("Duration", track.Duration.TotalHours >= 1 ? track.Duration.ToString(@"hh\:mm\:ss") : track.Duration.ToString(@"mm\:ss"), true)
            .AddField("Requested by", requesterName, true)
            .AddField("Requested at", item.RequestedAt.ToLocalTime().ToString("hh:mm tt", CultureInfo.InvariantCulture), true)
            .Build());
    }
}
