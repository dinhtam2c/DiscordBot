using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using Modules.Music.Tracks.Resolution;
using Modules.Music.Processes.YtDlp;

namespace Modules.Music.Tracks.Youtube;

internal sealed class YoutubeTrackResolver(
    IOptions<TrackSearchOptions> options,
    YtDlpClient ytDlpRunner) : ITrackResolver
{
    public TrackSource Source { get; } = TrackSource.Youtube;

    public bool CanResolveUrl(Uri uri)
    {
        var host = uri.IdnHost;
        return host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            || host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<TrackResolutionResult> ResolveAsync(string query, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(query, UriKind.Absolute, out var uri))
        {
            return await SearchFirstAsync(query, ct);
        }

        if (IsPlaylist(uri))
        {
            return await ResolvePlaylistAsync(query, ct);
        }

        return await ResolveVideoAsync(query, ct);
    }

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken ct = default)
    {
        var result = await ExecuteAsync(
            $"ytsearch{options.Value.SearchResultCount}:{query}",
            noPlaylist: true,
            ct);
        return result.Tracks;
    }

    private static bool IsPlaylist(Uri uri)
    {
        if (!uri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase))
            return false;

        var query = HttpUtility.ParseQueryString(uri.Query);

        var list = query["list"];
        if (string.IsNullOrEmpty(list))
            return false;

        if (list.StartsWith("RD", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private Task<TrackResolutionResult> SearchFirstAsync(string query, CancellationToken ct)
    {
        return ExecuteAsync(
            $"ytsearch1:{query}",
            noPlaylist: true,
            ct);
    }

    private Task<TrackResolutionResult> ResolveVideoAsync(string url, CancellationToken ct)
    {
        return ExecuteAsync(
            url,
            noPlaylist: true,
            ct);
    }

    private Task<TrackResolutionResult> ResolvePlaylistAsync(string url, CancellationToken ct)
    {
        return ExecuteAsync(
            url,
            noPlaylist: false,
            ct);
    }

    private async Task<TrackResolutionResult> ExecuteAsync(string input, bool noPlaylist, CancellationToken ct)
    {
        var arguments = new List<string> { "--dump-single-json" };

        if (noPlaylist)
            arguments.Add("--no-playlist");
        else
            arguments.Add("--flat-playlist");

        arguments.Add(input);

        var stdout = await ytDlpRunner.RunAsync(arguments, ct);
        return ParseResult(stdout, includePlaylist: !noPlaylist, input);
    }

    private static TrackResolutionResult ParseResult(string json, bool includePlaylist, string input)
    {
        var dto = JsonSerializer.Deserialize<YoutubeMetadata>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to parse YouTube metadata from yt-dlp output.");

        if (dto.Type == "playlist")
        {
            var tracks = dto.Entries?
                .Where(e => e is not null)
                .Select(ParseTrack)
                .ToList()
                ?? [];
            var playlist = includePlaylist
                ? new PlaylistInfo(string.IsNullOrWhiteSpace(dto.Title) ? "Unknown playlist" : dto.Title, dto.WebpageUrl ?? input,
                    dto.Uploader ?? dto.Channel, dto.UploaderUrl ?? dto.ChannelUrl)
                : null;
            return new TrackResolutionResult(tracks, playlist);
        }

        return new TrackResolutionResult([ParseTrack(dto)]);
    }

    private static Track ParseTrack(YoutubeMetadata dto)
    {
        var thumbnailUrl = dto.Thumbnail
            ?? dto.Thumbnails?.OrderByDescending(thumbnail => thumbnail.Width ?? 0).FirstOrDefault()?.Url
            ?? string.Empty;

        return new Track(
            Id: dto.Id,
            Title: string.IsNullOrWhiteSpace(dto.Title) ? "Unknown song" : dto.Title,
            Description: dto.Description ?? string.Empty,
            Artists: Track.SelectArtists(dto.Artists, [dto.Artist], dto.Creators, [dto.Creator], [dto.Channel], [dto.Uploader]),
            Duration: TimeSpan.FromSeconds(dto.Duration ?? 0),
            Source: TrackSource.Youtube,
            SourceUrl: dto.WebpageUrl ?? $"https://www.youtube.com/watch?v={dto.Id}",
            ThumbnailUrl: thumbnailUrl,
            UploaderName: dto.Channel ?? dto.Uploader,
            UploaderUrl: dto.ChannelUrl ?? dto.UploaderUrl);
    }
}
