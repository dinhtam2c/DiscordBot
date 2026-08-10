using Application.Discord.Voice;
using Modules.Music.Application.AudioSource;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players;

namespace Modules.Music.Infrastructure.Playback.NativePlayback;

internal sealed class NativePlaybackFactory(
    IAudioSourceResolver audioSourceResolver,
    IAudioTranscoder encoder)
    : IPlaybackFactory
{
    public async Task<IPlaybackInstance> StartAsync(Track track, IVoiceSession voiceSession, CancellationToken ct)
    {
        var source = audioSourceResolver.Resolve(track.Source);
        
        IPlaybackInstance instance = new NativePlaybackInstance(source, track, encoder, voiceSession);

        return instance;
    }
}
