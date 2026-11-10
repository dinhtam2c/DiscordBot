using Modules.Music.Processes.YtDlp;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modules.Music.Tracks.AudioSources;

internal abstract class YtDlpAudioSourceBase(YtDlpClient ytdlp) : IAudioSource
{
    public abstract TrackSource Source { get; }

    public async Task<string> GetUrlAsync(Track track, CancellationToken ct)
        => (await ResolveAsync(track, ct)).Url;

    public async Task<ResolvedAudioSource> ResolveAsync(Track track, CancellationToken ct)
    {
        var json = await ytdlp.RunAsync(
            ["--dump-single-json", "--no-playlist", "--format", "bestaudio", "--no-warnings", "--", track.SourceUrl], ct);
        var media = JsonSerializer.Deserialize<MediaInfo>(json)
            ?? throw new InvalidOperationException("Failed to parse audio source metadata from yt-dlp output.");
        if (!Uri.TryCreate(media.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("yt-dlp did not return an HTTP media URL.");

        // Like yt-dlp's downloader, wait until the selected format is available.
        // Opening a newly issued YouTube URL earlier can return 403; refreshing restarts the wait.
        if (media.AvailableAt is { } timestamp)
        {
            var delay = DateTimeOffset.UnixEpoch.AddSeconds(timestamp) - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct);
        }
        return new ResolvedAudioSource(media.Url!, media.Headers ?? [], media.IsLive);
    }

    public Task<Stream> OpenAsync(Track track, CancellationToken ct)
        => Task.FromResult(ytdlp.OpenStream(
            ["--output", "-", "--retries", "infinite", "--fragment-retries", "infinite",
             "--retry-sleep", "5", "--format", "bestaudio", "--quiet", "--no-warnings", "--", track.SourceUrl], ct));

    private sealed class MediaInfo
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }
        [JsonPropertyName("http_headers")]
        public Dictionary<string, string>? Headers { get; init; }
        [JsonPropertyName("is_live")]
        public bool IsLive { get; init; }
        [JsonPropertyName("available_at")]
        public double? AvailableAt { get; init; }
    }
}
