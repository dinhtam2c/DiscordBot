using Microsoft.Extensions.Options;
using Modules.Music.Tracks.Resolution;
using Modules.Music.Processes.YtDlp;
using System.Text.Json;

namespace Modules.Music.Tracks.SoundCloud;

internal sealed class SoundCloudTrackResolver(
    IOptions<TrackSearchOptions> searchOptions,
    YtDlpClient ytDlpRunner) : ITrackResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public TrackSource Source => TrackSource.SoundCloud;

    public bool CanResolveUrl(Uri uri)
    {
        var host = uri.IdnHost;
        return host.Equals("soundcloud.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".soundcloud.com", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<Track>> ResolveAsync(string query, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(query, UriKind.Absolute, out var uri) || !CanResolveUrl(uri))
            throw new ArgumentException("SoundCloud resolver requires a SoundCloud URL.", nameof(query));

        var json = await ytDlpRunner.RunAsync(
            ["--dump-single-json", "--no-warnings", query],
            ct);
        return ParseTracks(json, query);
    }

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken ct = default)
    {
        var input = $"scsearch{searchOptions.Value.SearchResultCount}:{query}";
        var json = await ytDlpRunner.RunAsync(
            ["--dump-single-json", "--flat-playlist", "--no-warnings", input],
            ct);

        return ParseTracks(json, fallbackUrl: null);
    }

    private static IReadOnlyList<Track> ParseTracks(string json, string? fallbackUrl)
    {
        var metadata = JsonSerializer.Deserialize<SoundCloudMetadata>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse SoundCloud metadata from yt-dlp output.");

        if (metadata.Entries is not null)
        {
            return metadata.Entries
                .Where(entry => entry is not null)
                .Select(entry => ParseTrack(entry, fallbackUrl))
                .ToList();
        }

        return [ParseTrack(metadata, fallbackUrl)];
    }

    private static Track ParseTrack(SoundCloudMetadata metadata, string? fallbackUrl)
    {
        var sourceUrl = metadata.WebpageUrl ?? metadata.OriginalUrl ?? fallbackUrl
            ?? throw new InvalidOperationException($"SoundCloud metadata for track {metadata.Id} did not include its webpage URL.");

        var thumbnailUrl = metadata.Thumbnail
            ?? metadata.Thumbnails?.OrderByDescending(thumbnail => thumbnail.Width ?? 0).FirstOrDefault()?.Url
            ?? string.Empty;

        return new Track(
            Id: metadata.Id,
            Title: metadata.Title,
            Description: metadata.Description ?? string.Empty,
            Artist: metadata.Artists?.FirstOrDefault() ?? metadata.Artist ?? metadata.Uploader ?? metadata.Channel ?? string.Empty,
            Duration: TimeSpan.FromSeconds(metadata.Duration ?? 0),
            Source: TrackSource.SoundCloud,
            SourceUrl: sourceUrl,
            ThumbnailUrl: thumbnailUrl);
    }
}
