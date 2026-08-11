using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Application.Playback;
using System.Diagnostics;

namespace Modules.Music.Infrastructure.Playback.NativePlayback.Ffmpeg;

internal sealed class FfmpegAudioTranscoder(
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegAudioTranscoder> logger) : IAudioTranscoder
{
    public Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct)
        => OpenStreamAsync(
            input: "pipe:0",
            configureInput: psi => psi.RedirectStandardInput = true,
            configureOutput: ConfigurePcmOutput,
            source,
            ct);

    public Task<Stream> OpenPcmStreamAsync(string source, CancellationToken ct)
        => OpenStreamAsync(
            input: source,
            configureInput: null,
            configureOutput: ConfigurePcmOutput,
            source: null,
            ct);

    public Task<Stream> OpenOpusStreamAsync(Stream source, CancellationToken ct)
        => OpenStreamAsync(
            input: "pipe:0",
            configureInput: psi => psi.RedirectStandardInput = true,
            configureOutput: ConfigureOpusOutput,
            source,
            ct);

    public Task<Stream> OpenOpusStreamAsync(string source, CancellationToken ct)
        => OpenStreamAsync(
            input: source,
            configureInput: null,
            configureOutput: ConfigureOpusOutput,
            source: null,
            ct);

    private Task<Stream> OpenStreamAsync(
        string input,
        Action<ProcessStartInfo>? configureInput,
        Action<ProcessStartInfo> configureOutput,
        Stream? source,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = options.Value.FfmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        configureInput?.Invoke(psi);

        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");

        // Reconnect HTTP input when using a direct media URL.
        if (source is null)
        {
            psi.ArgumentList.Add("-reconnect");
            psi.ArgumentList.Add("1");

            psi.ArgumentList.Add("-reconnect_streamed");
            psi.ArgumentList.Add("1");

            psi.ArgumentList.Add("-reconnect_delay_max");
            psi.ArgumentList.Add("5");
        }

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(input);

        configureOutput(psi);

        psi.ArgumentList.Add("pipe:1");

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg.");

        try
        {
            logger.LogDebug("Started ffmpeg process with PID {Pid}", process.Id);

            var stderrTask = DrainStderrAsync(process);

            Task? inputPump = null;

            if (source is not null)
            {
                inputPump = PumpAsync(source, process, ct);
            }

            Stream stream = new FfmpegStream(process, inputPump, stderrTask);

            return Task.FromResult(stream);
        }
        catch
        {
            TryKill(process);
            process.Dispose();
            throw;
        }
    }

    private static void ConfigurePcmOutput(ProcessStartInfo psi)
    {
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("s16le");

        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add("48000");

        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add("2");
    }

    private static void ConfigureOpusOutput(ProcessStartInfo psi)
    {
        psi.ArgumentList.Add("-vn");

        psi.ArgumentList.Add("-c:a");
        psi.ArgumentList.Add("libopus");

        psi.ArgumentList.Add("-application");
        psi.ArgumentList.Add("audio");

        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add("48000");

        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add("2");

        psi.ArgumentList.Add("-b:a");
        psi.ArgumentList.Add("128k");

        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("opus");
    }

    private async Task PumpAsync(
        Stream source,
        Process process,
        CancellationToken ct)
    {
        try
        {
            logger.LogTrace(
                "Pumping data to ffmpeg process with PID {Pid}",
                process.Id);

            await source.CopyToAsync(
                process.StandardInput.BaseStream,
                ct);

            await process.StandardInput.BaseStream.FlushAsync(ct);

            process.StandardInput.Close();

            logger.LogTrace(
                "Finished pumping data to ffmpeg PID {Pid}.",
                process.Id);
        }
        catch (OperationCanceledException)
        {
            logger.LogTrace("ffmpeg input pump cancelled, PID: {Pid}.", process.Id);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error while pumping data to ffmpeg PID {Pid}.", process.Id);

            try
            {
                process.StandardInput.Close();
            }
            catch
            {
            }

            throw;
        }
    }

    private async Task DrainStderrAsync(Process process)
    {
        try
        {
            var stderr = await process.StandardError.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                logger.LogWarning("ffmpeg PID {Pid} error output: {ErrorOutput}", process.Id, stderr);
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error while draining ffmpeg stderr PID {Pid}.", process.Id);

            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // Ignore cleanup failure.
        }
    }
}
