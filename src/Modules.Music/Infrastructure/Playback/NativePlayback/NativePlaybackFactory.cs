using Application.Discord.Voice;
using Modules.Music.Application.AudioSource;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players;
using Modules.Music.Infrastructure.Playback;

namespace Modules.Music.Infrastructure.Playback.NativePlayback;

internal sealed class NativePlaybackFactory(
    IAudioSourceResolver audioSourceResolver,
    IAudioTranscoder encoder)
    : IPlaybackFactory
{
    public IPlaybackInstance Start(Track track, IVoiceOutput voiceOutput, CancellationToken ct)
    {
        var source = audioSourceResolver.Resolve(track.Source);
        
        IPlaybackInstance instance = new NativePlaybackInstance(source, track, encoder, voiceOutput, ct);

        return instance;
    }
}
