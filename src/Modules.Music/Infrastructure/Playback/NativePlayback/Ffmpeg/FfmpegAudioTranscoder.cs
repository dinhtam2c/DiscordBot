using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Application.Playback;
using System.Diagnostics;

namespace Modules.Music.Infrastructure.Playback.NativePlayback.Ffmpeg;

internal class FfmpegAudioTranscoder(
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegAudioTranscoder> logger) : IAudioTranscoder
{
    public Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = options.Value.FfmpegPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("pipe:0");

        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("s16le");

        psi.ArgumentList.Add("-ar");
        psi.ArgumentList.Add("48000");

        psi.ArgumentList.Add("-ac");
        psi.ArgumentList.Add("2");

        psi.ArgumentList.Add("pipe:1");

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg.");

        try
        {
            logger.LogDebug("Started ffmpeg process with PID {Pid}", process.Id);

            var stderrTask = DrainStderrAsync(process);
            var inputPump = PumpAsync(source, process, ct);

            return Task.FromResult<Stream>(new FfmpegPcmStream(process, inputPump, stderrTask));
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch
            {
            }

            process.Dispose();
            throw;
        }
    }

    private async Task PumpAsync(Stream source, Process process, CancellationToken ct)
    {
        try
        {
            logger.LogTrace("Pumping data to ffmpeg process with PID {Pid}", process.Id);

            await source.CopyToAsync(process.StandardInput.BaseStream, ct);
            await process.StandardInput.BaseStream.FlushAsync(ct);
            process.StandardInput.Close();

            logger.LogTrace("Finished pumping data to ffmpeg PID {Pid}.", process.Id);
        }
        catch (OperationCanceledException)
        {
            logger.LogTrace("ffmpeg input pump cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error while pumping data to ffmpeg.");

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
                logger.LogWarning("ffmpeg error output: {ErrorOutput}", stderr);
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error while draining ffmpeg stderr.");
            throw;
        }
    }
}
