using Modules.Music.Tracks;
using Application.Discord.Voice;
using Modules.Music.Tracks.AudioSources;
using Modules.Music.Players;

namespace Modules.Music.Playback.Native;

internal sealed class NativePlaybackFactory(
    IAudioSourceResolver audioSourceResolver,
    IAudioTranscoder transcoder,
    Microsoft.Extensions.Logging.ILogger<NativePlaybackInstance> logger)
    : IPlaybackFactory
{
    public IPlaybackInstance Start(Track track, Volume volume, IVoiceOutput voiceOutput, CancellationToken ct,
        bool isPaused = false, TimeSpan startPosition = default, ResolvedAudioSource? source = null)
    {
        var audioSource = audioSourceResolver.Resolve(track.Source);

        return new NativePlaybackInstance(audioSource, track, volume, transcoder, voiceOutput, ct,
            isPaused, startPosition, source, logger);
    }
}
