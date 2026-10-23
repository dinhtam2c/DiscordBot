namespace Modules.Music.Infrastructure.Playback;

internal interface IAudioTranscoder
{
    Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct);

    Task<Stream> OpenPcmStreamAsync(string source, CancellationToken ct);

    Task<Stream> OpenOpusStreamAsync(Stream source, CancellationToken ct);

    Task<Stream> OpenOpusStreamAsync(string source, CancellationToken ct);
}
