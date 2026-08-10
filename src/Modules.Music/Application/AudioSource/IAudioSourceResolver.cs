using Modules.Music.Domain.Players;

namespace Modules.Music.Application.AudioSource;

internal interface IAudioSourceResolver
{
    IAudioSource Resolve(TrackSource source);
}
