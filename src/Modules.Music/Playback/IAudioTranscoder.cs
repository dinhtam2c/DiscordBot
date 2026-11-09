namespace Modules.Music.Playback;

internal interface IAudioTranscoder
{
    Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct);
}
