using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Exceptions;
using FFMpegCore.Pipes;
using System.Buffers;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Modules.Music.Processes;
using Modules.Music.Tracks.AudioSources;
using CliWrap.Builders;

namespace Modules.Music.Playback.Native.Ffmpeg;

internal sealed class FfmpegStream : ProducerStream
{
    public FfmpegStream(Stream source, string executablePath, ILogger logger, CancellationToken ct)
        : base((output, token) => TranscodeAsync(source, null, TimeSpan.Zero, output, executablePath, logger, token), ct) { }

    public FfmpegStream(ResolvedAudioSource source, TimeSpan position, string executablePath, ILogger logger, CancellationToken ct)
        : base((output, token) => TranscodeAsync(null, source, position, output, executablePath, logger, token), ct) { }

    private static async Task TranscodeAsync(Stream? source, ResolvedAudioSource? media, TimeSpan position,
        Stream output, string executablePath, ILogger logger, CancellationToken ct)
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

        var input = source is not null ? new AudioInput(source) : null;
        var pcm = new AudioOutput(output);
        FFMpegResult? result = null;
        try
        {
            try
            {
                var arguments = input is not null ? FFMpegArguments.FromPipeInput(input)
                    : FFMpegArguments.FromUrlInput(media!.Url, options =>
                    {
                        options.WithCustomArgument("-reconnect 1 -reconnect_streamed 1 -reconnect_on_network_error 1 -reconnect_on_http_error 408,429,5xx -reconnect_delay_max 5 -rw_timeout 15000000");
                        options.WithCustomArgument("-ss " + position.TotalSeconds.ToString("0.#######", System.Globalization.CultureInfo.InvariantCulture));
                        if (media.Headers.Count > 0)
                        {
                            if (media.Headers.Any(header => header.Key.IndexOfAny(['\r', '\n', ':']) >= 0 || header.Value.IndexOfAny(['\r', '\n']) >= 0))
                                throw new InvalidDataException("The audio source contains invalid HTTP headers.");
                            var headers = string.Concat(media.Headers.Select(header => $"{header.Key}: {header.Value}\r\n"));
                            options.WithCustomArgument("-headers " + new ArgumentsBuilder().Add(headers).Build());
                        }
                    });
                result = await arguments
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
                    if (input?.Error is { } sourceError && sourceError is not OperationCanceledException)
                        ExceptionDispatchInfo.Throw(sourceError);
                    if (pcm.Error is { } outputError && outputError is not OperationCanceledException)
                        ExceptionDispatchInfo.Throw(outputError);
                }
            }

            ct.ThrowIfCancellationRequested();
            if (media is not null)
            {
                var error = new AudioProcessException("ffmpeg", result.ExitCode, StandardError(result));
                if (error.IsTransient)
                    throw error;
            }
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
            if (media is not null && ex is FFMpegProcessException)
                throw new AudioProcessException("ffmpeg", result?.ExitCode ?? -1, StandardError(result));
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
