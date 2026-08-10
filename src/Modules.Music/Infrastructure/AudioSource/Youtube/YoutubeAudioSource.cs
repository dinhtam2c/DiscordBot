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

        logger.LogDebug("Started yt-dlp process with ID: {processId}", process.Id);

        _ = process.StandardError.ReadToEndAsync(ct).ContinueWith(t =>
        {
            if (!string.IsNullOrWhiteSpace(t.Result))
            {
                logger.LogWarning("yt-dlp error output: {errorOutput}", t.Result);
            }
        }, ct);

        return Task.FromResult<Stream>(new YtDlpStream(process));
    }
}
