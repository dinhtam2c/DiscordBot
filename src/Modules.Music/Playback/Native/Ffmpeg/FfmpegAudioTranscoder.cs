using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Modules.Music.Playback.Native.Ffmpeg;

internal sealed class FfmpegAudioTranscoder(
    IOptions<FfmpegOptions> options,
    ILogger<FfmpegAudioTranscoder> logger) : IAudioTranscoder
{
    public async Task<Stream> OpenPcmStreamAsync(Stream source, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

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

        ConfigurePcmOutput(psi);

        psi.ArgumentList.Add("pipe:1");

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg.");

        Task? inputPump = null;
        CancellationTokenSource? inputPumpLifetime = null;
        try
        {
            logger.LogDebug("Started ffmpeg process with PID {Pid}", process.Id);

            inputPumpLifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
            inputPump = PumpAsync(source, process, inputPumpLifetime.Token);

            Stream stream = new FfmpegStream(process, inputPump, inputPumpLifetime, logger);
            inputPumpLifetime = null;

            return stream;
        }
        catch
        {
            inputPumpLifetime?.Cancel();
            TryKill(process);
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            if (inputPump is not null)
            {
                try
                {
                    await inputPump.ConfigureAwait(false);
                }
                catch
                {
                }
            }

            inputPumpLifetime?.Dispose();
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
