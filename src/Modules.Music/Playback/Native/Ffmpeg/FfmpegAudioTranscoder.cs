using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Modules.Music.Playback.Native.Ffmpeg;

internal sealed class FfmpegAudioTranscoder(
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegAudioTranscoder> logger) : IAudioTranscoder
{
    public Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct)
        => Task.FromResult<Stream>(new FfmpegStream(source, options.Value.FfmpegPath, logger, ct));

    public Task<Stream> OpenPcmStreamAsync(Tracks.AudioSources.ResolvedAudioSource source, TimeSpan position, CancellationToken ct)
        => Task.FromResult<Stream>(new FfmpegStream(source, position, options.Value.FfmpegPath, logger, ct));
}
