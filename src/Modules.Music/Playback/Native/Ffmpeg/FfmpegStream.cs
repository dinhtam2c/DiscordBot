using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Exceptions;
using FFMpegCore.Pipes;
using System.Buffers;
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
        var pcm = new AudioOutput(output);
        FFMpegResult? result = null;
        try
        {
            try
            {
                result = await FFMpegArguments
                    .FromPipeInput(input)
                    .OutputToPipe(pcm, arguments => arguments
                        .ForceFormat("s16le")
                        .WithAudioCodec("pcm_s16le")
                        .WithAudioSamplingRate(48000)
                        .WithCustomArgument("-ac 2"))
                    .WithLogLevel(FFMpegLogLevel.Error)
                    .CancellableThrough(ct, TimeSpan.Zero)
                    .ProcessAsynchronously(throwOnError: true, ffOptions: settings);
            }
            catch (FFMpegProcessException ex)
            {
                result = ex.Result;
                throw;
            }
            finally
            {
                // Preserve pipe failures even when FFmpeg accepts partial audio or masks the cause.
                if (!ct.IsCancellationRequested)
                {
                    if (input.Error is { } sourceError && sourceError is not OperationCanceledException)
                        ExceptionDispatchInfo.Throw(sourceError);
                    if (pcm.Error is { } outputError && outputError is not OperationCanceledException)
                        ExceptionDispatchInfo.Throw(outputError);
                }
            }

            ct.ThrowIfCancellationRequested();
            if (pcm.BytesWritten == 0)
                throw new InvalidDataException("FFmpeg completed without producing PCM audio.");

            logger.LogDebug("FFmpeg completed with exit code {ExitCode}, producing {PcmBytes} bytes of PCM audio ({PcmDurationSeconds:F3} seconds). Standard error: {Stderr}",
                result.ExitCode, pcm.BytesWritten, pcm.Duration.TotalSeconds, StandardError(result));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("FFmpeg transcoding was canceled after producing {PcmBytes} bytes of PCM audio ({PcmDurationSeconds:F3} seconds).",
                pcm.BytesWritten, pcm.Duration.TotalSeconds);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to transcode audio. FFmpeg exit code: {ExitCode}. Produced {PcmBytes} bytes of PCM audio ({PcmDurationSeconds:F3} seconds). Standard error: {Stderr}",
                result?.ExitCode, pcm.BytesWritten, pcm.Duration.TotalSeconds, StandardError(result));
            throw;
        }
    }

    private static string StandardError(FFMpegResult? result)
    {
        var stderr = string.Join(Environment.NewLine, result?.StandardError ?? []).Trim();
        return stderr.Length > 8192 ? stderr[^8192..] : stderr;
    }

    private sealed class AudioOutput(Stream output) : IPipeSink
    {
        public long BytesWritten { get; private set; }
        public TimeSpan Duration => TimeSpan.FromSeconds(BytesWritten / (48000d * 2 * sizeof(short)));
        public Exception? Error { get; private set; }

        public string GetStreamArguments() => string.Empty;

        public async Task ReadAsync(Stream input, CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) != 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    BytesWritten += read;
                }
            }
            catch (Exception ex)
            {
                Error = ex;
                throw;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
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
