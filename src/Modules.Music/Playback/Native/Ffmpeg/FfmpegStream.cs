using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System.Runtime.ExceptionServices;
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

        var input = new AudioInput(source);
        try
        {
            await FFMpegArguments
                .FromPipeInput(input)
                .OutputToPipe(new StreamPipeSink(output), arguments => arguments
                    .ForceFormat("s16le")
                    .WithAudioCodec("pcm_s16le")
                    .WithAudioSamplingRate(48000)
                    .WithCustomArgument("-ac 2"))
                .WithLogLevel(FFMpegLogLevel.Error)
                .NotifyOnStandardError(line => logger.LogDebug("FFmpeg stderr: {Stderr}", line))
                .CancellableThrough(ct, TimeSpan.Zero)
                .ProcessAsynchronously(throwOnError: true, ffOptions: settings);
        }
        finally
        {
            // Preserve source failures even when FFmpeg reports invalid input or accepts partial audio.
            if (!ct.IsCancellationRequested && input.Error is { } error && error is not OperationCanceledException)
                ExceptionDispatchInfo.Throw(error);
        }
    }

    private sealed class AudioInput(Stream source) : IPipeSource
    {
        private readonly StreamPipeSource _input = new(source);
        public Exception? Error { get; private set; }

        public string GetStreamArguments() => _input.GetStreamArguments();

        public async Task WriteAsync(Stream output, CancellationToken ct)
        {
            try { await _input.WriteAsync(output, ct); }
            catch (Exception ex)
            {
                Error = ex;
                throw;
            }
        }
    }
}
