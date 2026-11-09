using Modules.Music.Processes.YtDlp;

namespace Modules.Music.Tracks.AudioSources;

internal abstract class YtDlpAudioSourceBase(YtDlpClient ytdlp) : IAudioSource
{
    public abstract TrackSource Source { get; }

    public async Task<string> GetUrlAsync(Track track, CancellationToken ct)
    {
        var url = (await ytdlp.RunAsync(
            ["--get-url", "--format", "bestaudio", "--quiet", "--no-warnings", "--", track.SourceUrl], ct)).Trim();
        return !string.IsNullOrWhiteSpace(url) ? url
            : throw new InvalidOperationException("yt-dlp returned an empty media URL.");
    }

    public Task<Stream> OpenAsync(Track track, CancellationToken ct)
        => Task.FromResult(ytdlp.OpenStream(
            ["--output", "-", "--retries", "infinite", "--fragment-retries", "infinite",
             "--retry-sleep", "5", "--format", "bestaudio", "--quiet", "--no-warnings", "--", track.SourceUrl], ct));
}
