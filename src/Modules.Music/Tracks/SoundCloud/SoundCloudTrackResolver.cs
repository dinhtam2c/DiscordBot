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

    public async Task<TrackResolutionResult> ResolveAsync(string query, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(query, UriKind.Absolute, out var uri) || !CanResolveUrl(uri))
            throw new ArgumentException("The SoundCloud track resolver requires a SoundCloud URL.", nameof(query));

        var json = await ytDlpRunner.RunAsync(
            ["--dump-single-json", "--no-warnings", query],
            ct);
        return ParseResult(json, query, includePlaylist: true);
    }

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken ct = default)
    {
        var input = $"scsearch{searchOptions.Value.SearchResultCount}:{query}";
        var json = await ytDlpRunner.RunAsync(
            ["--dump-single-json", "--flat-playlist", "--no-warnings", input],
            ct);

        return ParseResult(json, fallbackUrl: null, includePlaylist: false).Tracks;
    }

    private static TrackResolutionResult ParseResult(string json, string? fallbackUrl, bool includePlaylist)
    {
        var metadata = JsonSerializer.Deserialize<SoundCloudMetadata>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse SoundCloud metadata from yt-dlp output.");

        if (metadata.Entries is not null)
        {
            var tracks = metadata.Entries
                .Where(entry => entry is not null)
                .Select(entry => ParseTrack(entry, fallbackUrl))
                .ToList();
            var playlist = includePlaylist
                ? new PlaylistInfo(string.IsNullOrWhiteSpace(metadata.Title) ? "Unknown playlist" : metadata.Title, metadata.WebpageUrl ?? metadata.OriginalUrl ?? fallbackUrl
                        ?? throw new InvalidOperationException("SoundCloud playlist metadata does not contain a webpage URL."),
                    metadata.Uploader, metadata.UploaderUrl)
                : null;
            return new TrackResolutionResult(tracks, playlist);
        }

        return new TrackResolutionResult([ParseTrack(metadata, fallbackUrl)]);
    }

    private static Track ParseTrack(SoundCloudMetadata metadata, string? fallbackUrl)
    {
        var sourceUrl = metadata.WebpageUrl ?? metadata.OriginalUrl ?? fallbackUrl
            ?? throw new InvalidOperationException($"SoundCloud metadata for track '{metadata.Id}' does not contain a webpage URL.");

        var thumbnailUrl = metadata.Thumbnail
            ?? metadata.Thumbnails?.OrderByDescending(thumbnail => thumbnail.Width ?? 0).FirstOrDefault()?.Url
            ?? string.Empty;

        return new Track(
            Id: metadata.Id,
            Title: string.IsNullOrWhiteSpace(metadata.Title) ? "Unknown song" : metadata.Title,
            Description: metadata.Description ?? string.Empty,
            Artists: Track.SelectArtists(metadata.Artists, [metadata.Artist], [metadata.Uploader], [metadata.Channel]),
            Duration: TimeSpan.FromSeconds(metadata.Duration ?? 0),
            Source: TrackSource.SoundCloud,
            SourceUrl: sourceUrl,
            ThumbnailUrl: thumbnailUrl,
            UploaderName: metadata.Uploader ?? metadata.Channel,
            UploaderUrl: metadata.UploaderUrl)
        {
            CreditedArtists = Track.SelectArtists(metadata.Artists, [metadata.Artist]),
        };
    }
}
