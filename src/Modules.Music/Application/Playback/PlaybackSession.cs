using Application.Discord.Voice;
using Microsoft.Extensions.Logging;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players;

internal sealed class PlaybackSession(
    IPlaybackFactory playbackFactory,
    IVoiceSession voiceSession,
    Player player,
    ILogger<PlaybackSession> logger,
    CancellationToken externalToken) : IAsyncDisposable
{
    private readonly Lock _sync = new();

    private IPlaybackInstance? _playback;

    private Task? _loopTask;
    private readonly CancellationTokenSource _cancellation = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

    private bool _started = false;
    private bool _stopping = false;
    private bool _disposed = false;

    public Task Completion => _loopTask ?? Task.CompletedTask;

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
                throw new InvalidOperationException("Playback session has already been started.");

            ObjectDisposedException.ThrowIf(_stopping || _disposed, this);

            _started = true;
            _loopTask = RunLoopAsync();
        }
    }

    private async Task RunLoopAsync()
    {
        var ct = _cancellation.Token;

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

                var playback = playbackFactory.Start(track, voiceSession, ct);
                if (!TryAttachPlayback(playback))
                {
                    await playback.DisposeAsync();
                    break;
                }

                await playback.Completion;

                logger.LogInformation("Track finished for guild {GuildId}, Track: {TrackTitle}", player.GuildId, track.Title);

                player.FinishCurrentTrack();
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Playback-level cancellation, e.g. skip.
                logger.LogInformation("Playback cancelled for guild {GuildId}, Track: {TrackTitle}", player.GuildId, track.Title);
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Playback session cancelled for guild {GuildId}.", player.GuildId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while playing track for guild {GuildId}.", player.GuildId);
                player.FinishCurrentTrack();
            }
            finally
            {
                await StopCurrentPlaybackAsync();
            }
        }

        _loopTask = null;
    }

    private bool TryAttachPlayback(IPlaybackInstance playback)
    {
        lock (_sync)
        {
            if (!_started || _stopping || _disposed)
                return false;

            _playback = playback;
            return true;
        }
    }

    public async Task StopCurrentPlaybackAsync()
    {
        IPlaybackInstance? playback;

        lock (_sync)
        {
            playback = _playback;
            _playback = null;
        }

        if (playback is not null)
            await playback.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _stopping = true;
            _disposed = true;
        }

        _cancellation.Cancel();

        await StopCurrentPlaybackAsync();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellation.Dispose();
    }
}
