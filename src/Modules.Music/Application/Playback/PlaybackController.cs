using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Modules.Music.Domain.Players;
using System.Collections.Concurrent;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackController(
    IPlaybackFactory playbackFactory,
    ILogger<PlaybackController> logger)
{
    private readonly ConcurrentDictionary<GuildId, PlaybackRuntime> _runtimes = [];

    public Task StartAsync(Player player, IVoiceSession voiceSession, CancellationToken ct)
    {
        var runtime = _runtimes.GetOrAdd(player.GuildId, _ => new PlaybackRuntime(voiceSession));

        lock (runtime)
        {
            if (runtime.LoopTask is not null && !runtime.LoopTask.IsCompleted)
            {
                return Task.CompletedTask;
            }

            runtime.LoopTask = RunLoopAsync(player, runtime, ct);
        }

        return Task.CompletedTask;
    }

    public async Task StopCurrentAsync(GuildId guildId, CancellationToken ct)
    {
        if (!_runtimes.TryGetValue(guildId, out var runtime))
            return;

        await runtime.StopPlaybackAsync();
    }

    public async Task StopAsync(GuildId guildId, CancellationToken ct)
    {
        if (!_runtimes.TryRemove(guildId, out var runtime))
            return;

        runtime.Cancellation.Cancel();

        await runtime.StopPlaybackAsync();

        if (runtime.LoopTask is not null)
        {
            try
            {
                await runtime.LoopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        runtime.Cancellation.Dispose();
    }

    private async Task RunLoopAsync(Player player, PlaybackRuntime runtime, CancellationToken externalCt)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(runtime.Cancellation.Token, externalCt);

        var ct = linkedCts.Token;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!player.HasCurrentTrack)
                {
                    logger.LogInformation("Queue finished for guild {GuildId}.", player.GuildId);
                    break;
                }

                var track = player.CurrentTrack!;

                try
                {
                    logger.LogInformation("Starting playback for guild {GuildId}, Track: {TrackTitle}", player.GuildId, track.Title);

                    var playback = await playbackFactory.StartAsync(track, runtime.VoiceSession, ct);
                    runtime.AttachPlayback(playback);

                    await playback.Completion;

                    logger.LogInformation("Track finished for guild {GuildId}, Track: {TrackTitle}", player.GuildId, track.Title);

                    player.FinishCurrentTrack();
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Playback cancelled for guild {GuildId}.", player.GuildId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred while playing track for guild {GuildId}.", player.GuildId);
                    player.FinishCurrentTrack();
                }
                finally
                {
                    await runtime.StopPlaybackAsync();
                }
            }
        }
        finally
        {
            runtime.LoopTask = null;
        }
    }

    private sealed class PlaybackRuntime(IVoiceSession voiceSession)
    {
        private readonly object _sync = new();

        public IVoiceSession VoiceSession { get; } = voiceSession;

        public CancellationTokenSource Cancellation { get; } = new();

        public Task? LoopTask { get; set; }

        private IPlaybackInstance? _playback;

        public void AttachPlayback(IPlaybackInstance playback)
        {
            lock (_sync)
            {
                _playback = playback;
            }
        }

        public async Task StopPlaybackAsync()
        {
            IPlaybackInstance? playback;

            lock (_sync)
            {
                playback = _playback;
                _playback = null;
            }

            if (playback is null)
                return;

            await playback.DisposeAsync();
        }
    }
}