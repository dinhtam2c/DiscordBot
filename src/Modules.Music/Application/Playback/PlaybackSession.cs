using Application.Discord.Voice;
using Domain.ValueObjects;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackSession(GuildId guildId, IVoiceSession session) : IAsyncDisposable
{
    public GuildId GuildId { get; } = guildId;

    public IVoiceSession VoiceSession { get; } = session;

    public IPlaybackInstance? Playback { get; private set; }

    public bool IsPlaying => Playback?.IsPlaying == true;

    public bool IsPaused { get; private set; }

    public void AttachPlayback(IPlaybackInstance playback)
    {
        Playback = playback;
    }

    public async Task StopPlaybackAsync()
    {
        if (Playback is null)
            return;

        await Playback.DisposeAsync();

        Playback = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopPlaybackAsync();
    }
}
