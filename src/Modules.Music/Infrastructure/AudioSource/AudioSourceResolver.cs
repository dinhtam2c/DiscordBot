using Modules.Music.Application.AudioSource;
using Modules.Music.Domain.Players;

namespace Modules.Music.Infrastructure.AudioSource;

internal class AudioSourceResolver(IEnumerable<IAudioSource> audioSources) : IAudioSourceResolver
{
    private readonly Dictionary<TrackSource, IAudioSource> _audioSources = audioSources.ToDictionary(a => a.Source);

    public IAudioSource Resolve(TrackSource source)
    {
        if (!_audioSources.TryGetValue(source, out var audioSource))
        {
            throw new InvalidOperationException($"No audio source found for {source}");
        }

        return audioSource;
    }
}
