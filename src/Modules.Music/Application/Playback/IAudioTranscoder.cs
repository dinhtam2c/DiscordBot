namespace Modules.Music.Application.Playback;

internal interface IAudioTranscoder
{
    Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct);
}
