using Modules.Music.Tracks;
using Application.Discord.Voice;
using Modules.Music.Tracks.AudioSources;
using Modules.Music.Players;

namespace Modules.Music.Playback.Native;

internal sealed class NativePlaybackFactory(
    IAudioSourceResolver audioSourceResolver,
    IAudioTranscoder transcoder)
    : IPlaybackFactory
{
    public IPlaybackInstance Start(Track track, Volume volume, IVoiceOutput voiceOutput, CancellationToken ct)
    {
        var source = audioSourceResolver.Resolve(track.Source);

        return new NativePlaybackInstance(source, track, volume, transcoder, voiceOutput, ct);
    }
}
