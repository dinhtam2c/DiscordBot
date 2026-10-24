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
    public IPlaybackInstance Start(Track track, Volume volume, IVoiceOutput voiceOutput, CancellationToken ct)
    {
        var source = audioSourceResolver.Resolve(track.Source);

        IPlaybackInstance instance = new NativePlaybackInstance(source, track, volume, encoder, voiceOutput, ct);

        return instance;
    }
}
