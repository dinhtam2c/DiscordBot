namespace Modules.Music.Tracks.AudioSources;

internal interface IAudioSourceResolver
{
    IAudioSource Resolve(TrackSource source);
}
