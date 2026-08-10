using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Application.AudioSource;
using Modules.Music.Domain.Players;
using System.Diagnostics;

namespace Modules.Music.Infrastructure.AudioSource.Youtube;

internal class YoutubeAudioSource(
    IOptions<YoutubeOptions> options,
    ILogger<YoutubeAudioSource> logger) : IAudioSource
{
    public TrackSource Source => TrackSource.Youtube;

    public Task<Stream> OpenAsync(Track track, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = options.Value.YtDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("--output");
        psi.ArgumentList.Add("-");

        psi.ArgumentList.Add("--format");
        psi.ArgumentList.Add("ba/b");

        psi.ArgumentList.Add("--extractor-args");
        psi.ArgumentList.Add("youtube:player_client=android,ios,web");

        psi.ArgumentList.Add("--quiet");
        psi.ArgumentList.Add("--no-warnings");

        psi.ArgumentList.Add(track.SourceUrl);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        try
        {
            logger.LogDebug("Started yt-dlp process with ID: {processId}", process.Id);

            var stderrTask = DrainStderrAsync(process);

            return Task.FromResult<Stream>(new YtDlpStream(process, stderrTask));
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

    private async Task DrainStderrAsync(Process process)
    {
        try
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                logger.LogWarning("yt-dlp error output: {errorOutput}", stderr);
            }
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Error while draining yt-dlp stderr.");
            throw;
        }
    }
}
