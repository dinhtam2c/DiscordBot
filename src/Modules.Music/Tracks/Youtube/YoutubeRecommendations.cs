using System.Text.RegularExpressions;
using Modules.Music.Processes.YtDlp;
using System.Text.Json;

namespace Modules.Music.Tracks.Youtube;

internal interface IYoutubeRecommendations
{
    Task<IReadOnlyList<Track>> GetAsync(Track seed, int limit, CancellationToken ct);
    Task<Track?> ResolveAsync(Track candidate, CancellationToken ct);
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
            anchor = ParseCandidates(search)
                .Where(track => SimilarTitle(seed, track) >= .65 && DurationMatches(seed, track, 15))
                .OrderByDescending(track => SimilarTitle(seed, track)).FirstOrDefault()
                ?? throw new Application.Abstractions.UserFriendlyException("Couldn't find a matching YouTube song for autoplay.");
        }
        var url = $"https://www.youtube.com/watch?v={Uri.EscapeDataString(anchor.Id)}&list=RD{Uri.EscapeDataString(anchor.Id)}";
        var json = await ytdlp.RunAsync(["--dump-single-json", "--flat-playlist", "--playlist-end", limit.ToString(), "--no-warnings", "--", url], ct);
        return ParseCandidates(json)
            .Where(track => !SameSong(anchor, track) && Suitable(seed, track)).ToArray();
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
            if (title.Contains(version) && !seed.Title.Contains(version, StringComparison.OrdinalIgnoreCase)) return false;
        return seed.Duration.TotalMinutes >= 15 || (track.Duration.TotalMinutes < 15
            && !Regex.IsMatch(title, @"\b(compilation|playlist|nonstop|1 hour|10 hours)\b"));
    }

    public static bool SameSong(Track left, Track right)
        => left.Source == right.Source && left.Id == right.Id
            || DurationMatches(left, right, 10) && SimilarTitle(left, right) >= .85;

    private static bool DurationMatches(Track left, Track right, int tolerance)
        => left.Duration > TimeSpan.Zero && right.Duration > TimeSpan.Zero
            && Math.Abs((left.Duration - right.Duration).TotalSeconds) <= tolerance;

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
