using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using Microsoft.Extensions.Logging;
using Modules.Music.Processes;

namespace Modules.Music.Playback.Native.Ffmpeg;

internal sealed class FfmpegStream(Stream source, string executablePath, ILogger logger, CancellationToken ct)
    : ProducerStream((output, token) => TranscodeAsync(source, output, executablePath, logger, token), ct)
{
    private static async Task TranscodeAsync(Stream source, Stream output, string executablePath, ILogger logger, CancellationToken ct)
    {
        var name = Path.GetFileName(executablePath);
        if (!string.Equals(name, "ffmpeg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("FfmpegPath must point to an executable named ffmpeg or ffmpeg.exe.");
        var directory = Path.GetDirectoryName(executablePath);
        if (!string.IsNullOrWhiteSpace(directory) && !File.Exists(executablePath))
            throw new FileNotFoundException("The configured FFmpeg executable was not found.", executablePath);
        var settings = new FFOptions
        {
            BinaryFolder = string.IsNullOrWhiteSpace(directory) ? string.Empty : Path.GetFullPath(directory),
        };

        await FFMpegArguments
            .FromPipeInput(new StreamPipeSource(source))
            .OutputToPipe(new StreamPipeSink(output), arguments => arguments
                .ForceFormat("s16le")
                .WithAudioCodec("pcm_s16le")
                .WithAudioSamplingRate(48000)
                .WithCustomArgument("-ac 2"))
            .WithLogLevel(FFMpegLogLevel.Error)
            .NotifyOnError(line => logger.LogDebug("FFmpeg stderr: {Stderr}", line))
            .CancellableThrough(ct)
            .ProcessAsynchronously(throwOnError: true, ffMpegOptions: settings);
    }
}
