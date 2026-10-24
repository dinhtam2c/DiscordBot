using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Application.AudioSource;
using Modules.Music.Domain.Players;
using Modules.Music.Infrastructure.Processes.YtDlp;

namespace Modules.Music.Infrastructure.AudioSource;

internal abstract class YtDlpAudioSourceBase(
    IOptions<YtDlpOptions> options,
    ILogger<YtDlpAudioSourceBase> logger) : IAudioSource
{
    public abstract TrackSource Source { get; }

    public async Task<string> GetUrlAsync(Track track, CancellationToken ct)
    {
        var psi = CreateProcessStartInfo();
        psi.ArgumentList.Add("--get-url");
        ConfigureCommonArguments(psi, track);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        try
        {
            logger.LogDebug("Started yt-dlp process with PID {Pid} for URL extraction.", process.Id);
            stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var url = (await stdoutTask).Trim();
            var stderr = await stderrTask;
            if (!string.IsNullOrWhiteSpace(stderr))
                logger.LogDebug("yt-dlp stderr: {ErrorOutput}", stderr);

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"yt-dlp exited with code {process.ExitCode}.");

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("yt-dlp returned an empty media URL.");

            logger.LogDebug("yt-dlp successfully extracted media URL.");
            return url;
        }
        catch
        {
            await StopProcessAsync(process, stdoutTask, stderrTask);
            throw;
        }
        finally
        {
            process.Dispose();
        }
    }

    public async Task<Stream> OpenAsync(Track track, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var psi = CreateProcessStartInfo();
        psi.ArgumentList.Add("--output");
        psi.ArgumentList.Add("-");
        psi.ArgumentList.Add("--retries");
        psi.ArgumentList.Add("infinite");
        psi.ArgumentList.Add("--fragment-retries");
        psi.ArgumentList.Add("infinite");
        psi.ArgumentList.Add("--retry-sleep");
        psi.ArgumentList.Add("5");
        ConfigureCommonArguments(psi, track);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        try
        {
            logger.LogDebug("Started yt-dlp process with PID {Pid} for {Source}.", process.Id, Source);
            return new YtDlpStream(process, logger);
        }
        catch
        {
            TryKill(process);
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            process.Dispose();
            throw;
        }
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
            FileName = options.Value.ExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Continue cleanup even if the process has already exited or cannot be killed.
        }
    }

    private static async Task StopProcessAsync(Process process, params Task?[] pendingTasks)
    {
        TryKill(process);
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
        }

        foreach (var pendingTask in pendingTasks)
        {
            if (pendingTask is null)
                continue;

            try
            {
                await pendingTask.ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
