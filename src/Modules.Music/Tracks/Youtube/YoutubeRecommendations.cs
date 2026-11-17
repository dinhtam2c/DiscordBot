using System.Text.RegularExpressions;
using Modules.Music.Processes.YtDlp;
using System.Text.Json;

namespace Modules.Music.Tracks.Youtube;

internal interface IYoutubeRecommendations
{
    Task<IReadOnlyList<Track>> GetAsync(Track seed, int limit, CancellationToken ct);
    Task<Track?> ResolveAsync(Track candidate, CancellationToken ct);
    Task<IReadOnlyList<Track>> ExploreAsync(Track seed, int attempt, CancellationToken ct);
}

internal sealed class YoutubeRecommendations(YtDlpClient ytdlp) : IYoutubeRecommendations
{
    public async Task<IReadOnlyList<Track>> GetAsync(Track seed, int limit, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return await GetCoreAsync(seed, limit, ct); }
            catch (Exception ex) when (attempt < 2 && Modules.Music.Processes.AudioProcessException.IsTransientFailure(ex)
                && !ct.IsCancellationRequested)
            { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), ct); }
        }
    }

    private async Task<IReadOnlyList<Track>> GetCoreAsync(Track seed, int limit, CancellationToken ct)
    {
        var anchor = seed;
        if (seed.Source != TrackSource.Youtube)
        {
            var query = string.Join(" ", seed.Artists) + " " + seed.Title;
            var search = await ytdlp.RunAsync(["--dump-single-json", "--flat-playlist", "--no-warnings", $"ytsearch5:{query}"], ct);
            var matches = ParseCandidates(search).ToArray();
            anchor = matches
                .Where(track => SimilarTitle(seed, track) >= .65 && DurationMatches(seed, track, 15))
                .OrderByDescending(track => SimilarTitle(seed, track)).FirstOrDefault()
                ?? matches.Where(track => Suitable(seed, track)).OrderByDescending(track => SimilarTitle(seed, track)).FirstOrDefault()!;
            if (anchor is null) return [];
        }
        var url = $"https://www.youtube.com/watch?v={Uri.EscapeDataString(anchor.Id)}&list=RD{Uri.EscapeDataString(anchor.Id)}";
        var json = await ytdlp.RunAsync(["--dump-single-json", "--flat-playlist", "--playlist-end", limit.ToString(), "--no-warnings", "--", url], ct);
        var mix = JsonSerializer.Deserialize<YoutubeMetadata>(json);
        if (mix?.Entries is { Count: > 0 })
            return ParseCandidates(json)
                .Where(track => !SameSong(anchor, track) && Suitable(seed, track)).ToArray();

        // Some radio URLs silently resolve to the seed video, with no playlist entries.
        // Only fall back to closely named videos from the same uploader (for example a numbered series).
        var related = await ytdlp.RunAsync(
            ["--dump-single-json", "--flat-playlist", "--no-warnings", $"ytsearch{limit}:{anchor.Title}"], ct);
        var candidates = ParseCandidates(related)
            .Where(track => !SameSong(anchor, track) && SameUploader(anchor, track)
                && SimilarTitle(anchor, track) >= .6 && Suitable(seed, track))
            .ToArray();
        // At the wider tier, continuity takes priority over uploader/title restrictions.
        return candidates.Length > 0 || limit < 75 ? candidates
            : ParseCandidates(related).Where(track => Suitable(seed, track)).ToArray();
    }

    public async Task<IReadOnlyList<Track>> ExploreAsync(Track seed, int attempt, CancellationToken ct)
    {
        // Widen queries and rotate discovery anchors; never return a history item as a playback fallback.
        var title = Regex.Replace(seed.Title, @"[_|]+", " ");
        title = Regex.Replace(title, @"\b(vol(?:ume)?|part|episode)\s*\d+\b", "", RegexOptions.IgnoreCase).Trim();
        var artists = string.Join(" ", seed.Artists);
        var queries = new[] { title, artists + " music", string.Join(" ", title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3)) + " music" }
            .Where(query => !string.IsNullOrWhiteSpace(query)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var count = Math.Min(200, 25 + (long)attempt * 25);
        var json = await ytdlp.RunAsync(["--dump-single-json", "--flat-playlist", "--no-warnings",
            $"ytsearch{count}:{queries[attempt % queries.Length]}"], ct);
        return ParseCandidates(json).Where(track => !SameSong(seed, track) && Suitable(seed, track)).ToArray();
    }

    public async Task<Track?> ResolveAsync(Track candidate, CancellationToken ct)
    {
        var json = await ytdlp.RunAsync(["--dump-single-json", "--no-playlist", "--no-warnings", "--", candidate.SourceUrl], ct);
        var metadata = JsonSerializer.Deserialize<YoutubeMetadata>(json);
        return metadata is null || metadata.IsLive == true || metadata.Availability == "private"
            ? null : YoutubeTrackResolver.ParseTrack(metadata);
    }

    private static IEnumerable<Track> ParseCandidates(string json)
        => (JsonSerializer.Deserialize<YoutubeMetadata>(json)?.Entries ?? [])
            .Where(metadata => !string.IsNullOrWhiteSpace(metadata.Id) && !string.IsNullOrWhiteSpace(metadata.Title) && metadata.IsLive != true && metadata.Availability != "private")
            .Select(YoutubeTrackResolver.ParseTrack);

    public static bool Suitable(Track seed, Track track)
    {
        if (track.Duration <= TimeSpan.Zero) return false;
        var title = track.Title.ToLowerInvariant();
        foreach (var version in new[] { "cover", "acoustic", "sped up", "slowed", "nightcore", "karaoke" })
        {
            var pattern = @"\b" + Regex.Escape(version) + @"\b";
            if (Regex.IsMatch(title, pattern) && !Regex.IsMatch(seed.Title, pattern, RegexOptions.IgnoreCase)) return false;
        }
        return seed.Duration.TotalMinutes >= 15 || (track.Duration.TotalMinutes < 15
            && !Regex.IsMatch(title, @"\b(compilation|playlist|nonstop|1 hour|10 hours)\b"));
    }

    public static bool SameSong(Track left, Track right)
        => left.Source == right.Source && left.Id == right.Id
            || DurationMatches(left, right, 10) && SimilarTitle(left, right) >= .85;

    private static bool DurationMatches(Track left, Track right, int tolerance)
        => left.Duration > TimeSpan.Zero && right.Duration > TimeSpan.Zero
            && Math.Abs((left.Duration - right.Duration).TotalSeconds) <= tolerance;

    private static bool SameUploader(Track left, Track right)
        => !string.IsNullOrWhiteSpace(left.UploaderUrl) && !string.IsNullOrWhiteSpace(right.UploaderUrl)
            && string.Equals(left.UploaderUrl.TrimEnd('/'), right.UploaderUrl.TrimEnd('/'), StringComparison.Ordinal);

    private static double SimilarTitle(Track left, Track right)
    {
        var a = Words(left); var b = Words(right);
        return a.Count == 0 || b.Count == 0 ? 0 : (double)a.Intersect(b).Count() / a.Union(b).Count();
    }

    private static HashSet<string> Words(Track track)
    {
        var title = track.Title.ToLowerInvariant();
        foreach (var artist in track.Artists.OrderByDescending(artist => artist.Length))
            if (title.StartsWith(artist.ToLowerInvariant() + " - ")) title = title[(artist.Length + 3)..];
        title = Regex.Replace(title, @"\b(official|music|video|audio|lyrics?|visualizer|mv|hd|4k)\b", " ");
        return Regex.Matches(title, @"[\p{L}\p{N}]+").Select(match => match.Value).ToHashSet();
    }
}
