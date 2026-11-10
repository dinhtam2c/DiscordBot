namespace Modules.Music.Playback;

internal interface IAudioTranscoder
{
    Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct);

    Task<Stream> OpenPcmStreamAsync(Tracks.AudioSources.ResolvedAudioSource source, TimeSpan position, CancellationToken ct);
}
