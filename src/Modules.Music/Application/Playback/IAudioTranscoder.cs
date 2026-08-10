namespace Modules.Music.Application.Playback;

internal interface IAudioTranscoder
{
    Task<Stream> TranscodePcmAsync(Stream source, CancellationToken ct);
}
