using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Application.AudioSource;
using Modules.Music.Domain.Players;
using System.Diagnostics;

namespace Modules.Music.Infrastructure.AudioSource.Youtube;

internal sealed class YoutubeAudioSource(
    IOptions<YoutubeOptions> options,
    ILogger<YoutubeAudioSource> logger) : IAudioSource
{
    public TrackSource Source => TrackSource.Youtube;

    public async Task<string> GetUrlAsync(Track track, CancellationToken ct)
    {
        var psi = CreateProcessStartInfo();

        ConfigureUrlExtraction(psi, track);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        try
        {
            logger.LogDebug("Started yt-dlp process with PID {Pid} for URL extraction.", process.Id);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var url = (await stdoutTask).Trim();
            var stderr = await stderrTask;

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                logger.LogDebug("yt-dlp stderr: {ErrorOutput}", stderr);
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"yt-dlp exited with code {process.ExitCode}.");
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("yt-dlp returned an empty media URL.");
            }

            logger.LogDebug("yt-dlp successfully extracted media URL.");

            return url;
        }
        catch (OperationCanceledException)
        {
            logger.LogTrace("yt-dlp URL extraction cancelled.");

            TryKill(process);
            throw;
        }
        catch
        {
            TryKill(process);
            throw;
        }
        finally
        {
            process.Dispose();
        }
    }

    public Task<Stream> OpenAsync(Track track, CancellationToken ct)
    {
        var psi = CreateProcessStartInfo();

        ConfigureStreamDownload(psi, track);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        try
        {
            logger.LogDebug("Started yt-dlp process with PID {Pid}.", process.Id);

            var stderrTask = DrainStderrAsync(process, ct);

            Stream stream = new YtDlpStream(process, stderrTask);

            return Task.FromResult(stream);
        }
        catch
        {
            TryKill(process);
            process.Dispose();
            throw;
        }
    }

    private static void ConfigureUrlExtraction(ProcessStartInfo psi, Track track)
    {
        psi.ArgumentList.Add("--get-url");

        ConfigureCommonArguments(psi, track);
    }

    private static void ConfigureStreamDownload(ProcessStartInfo psi, Track track)
    {
        psi.ArgumentList.Add("--output");
        psi.ArgumentList.Add("-");

        psi.ArgumentList.Add("--retries");
        psi.ArgumentList.Add("infinite");

        psi.ArgumentList.Add("--fragment-retries");
        psi.ArgumentList.Add("infinite");

        psi.ArgumentList.Add("--retry-sleep");
        psi.ArgumentList.Add("5");

        ConfigureCommonArguments(psi, track);
    }

    private static void ConfigureCommonArguments(ProcessStartInfo psi, Track track)
    {
        psi.ArgumentList.Add("--format");
        psi.ArgumentList.Add("bestaudio");

        psi.ArgumentList.Add("--quiet");
        psi.ArgumentList.Add("--no-warnings");

        psi.ArgumentList.Add(track.SourceUrl);
    }

    private ProcessStartInfo CreateProcessStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = options.Value.YtDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private async Task DrainStderrAsync(Process process, CancellationToken ct)
    {
        try
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            if (!string.IsNullOrWhiteSpace(stderr))
                logger.LogWarning("yt-dlp error output: {ErrorOutput}", stderr);
        }
        catch (OperationCanceledException)
        {
            logger.LogTrace("yt-dlp stderr draining cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error while draining yt-dlp stderr.");
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
