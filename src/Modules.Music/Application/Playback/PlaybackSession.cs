using Application.Discord.Voice;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackSession(
    IPlaybackFactory playbackFactory,
    IVoiceSession voiceSession,
    Track track,
    CancellationToken externalToken) : IAsyncDisposable
{
    private IPlaybackInstance? _playback;

    public Task Completion { get; private set; } = Task.CompletedTask;

    public void Start()
    {
        if (_playback is not null)
            throw new InvalidOperationException("Playback session has already been started.");

        _playback = playbackFactory.Start(track, voiceSession, externalToken);
        Completion = _playback.Completion;
    }

    public async ValueTask DisposeAsync()
    {
        if (_playback is not null)
            await _playback.DisposeAsync();
    }
}
