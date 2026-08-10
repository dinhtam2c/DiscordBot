using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Music.Application.TrackResolver;
using Modules.Music.Domain.Players;
using Modules.Music.Infrastructure.AudioSource.Youtube;
using System.Diagnostics;
using System.Text.Json;
using System.Web;

namespace Modules.Music.Infrastructure.TrackResolver;

internal sealed class YoutubeTrackResolver(
    IOptions<YoutubeOptions> options,
    ILogger<YoutubeTrackResolver> logger) : ITrackResolver
{
    public TrackSource Source { get; } = TrackSource.Youtube;

    public async Task<IReadOnlyList<Track>> ResolveAsync(string query, CancellationToken ct = default)
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

    public Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken ct = default)
    {
        return ExecuteAsync(
            $"ytsearch{options.Value.SearchResultCount}:{query}",
            noPlaylist: true,
            ct);
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

    private Task<IReadOnlyList<Track>> SearchFirstAsync(string query, CancellationToken ct)
    {
        return ExecuteAsync(
            $"ytsearch1:{query}",
            noPlaylist: true,
            ct);
    }

    private Task<IReadOnlyList<Track>> ResolveVideoAsync(string url, CancellationToken ct)
    {
        return ExecuteAsync(
            url,
            noPlaylist: true,
            ct);
    }

    private Task<IReadOnlyList<Track>> ResolvePlaylistAsync(string url, CancellationToken ct)
    {
        return ExecuteAsync(
            url,
            noPlaylist: false,
            ct);
    }

    private async Task<IReadOnlyList<Track>> ExecuteAsync(string input, bool noPlaylist, CancellationToken ct, bool flatPlaylist = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = options.Value.YtDlpPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("--dump-single-json");

        if (noPlaylist)
            psi.ArgumentList.Add("--no-playlist");

        if (flatPlaylist)
            psi.ArgumentList.Add("--flat-playlist");

        psi.ArgumentList.Add("--extractor-args");
        psi.ArgumentList.Add("youtube:player_client=android,ios,web");

        psi.ArgumentList.Add(input);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start yt-dlp.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            logger.LogError("yt-dlp stderr: {stderr}", stderr);
            throw new InvalidOperationException($"yt-dlp exited with code {process.ExitCode}");
        }

        return ParseTracks(stdout);
    }

    private static IReadOnlyList<Track> ParseTracks(string json)
    {
        var dto = JsonSerializer.Deserialize<YoutubeMetadata>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to parse yt-dlp output.");

        if (dto.Type == "playlist")
        {
            return dto.Entries?
                .Where(e => e is not null)
                .Select(ParseTrack)
                .ToList()
                ?? [];
        }

        return [ParseTrack(dto)];
    }

    private static Track ParseTrack(YoutubeMetadata dto)
    {
        return new Track(
            Id: dto.Id,
            Title: dto.Title,
            Description: dto.Description ?? string.Empty,
            Artist: dto.Channel ?? string.Empty,
            Duration: TimeSpan.FromSeconds(dto.Duration ?? 0),
            Source: TrackSource.Youtube,
            SourceUrl: dto.WebpageUrl ?? $"https://www.youtube.com/watch?v={dto.Id}",
            ThumbnailUrl: dto.Thumbnail ?? string.Empty);
    }
}
