using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Modules.Music.Features.Autoplay;
using Modules.Music.Processes.YtDlp;

namespace Modules.Music.Tracks.Youtube;

internal interface IYoutubeRecommendations
{
    Task<IReadOnlyList<AutoplayCandidate>> GetAsync(Track seed, int limit, CancellationToken ct);
    Task<Track?> ResolveAsync(Track candidate, CancellationToken ct);
    Task<IReadOnlyList<AutoplayCandidate>> ExploreAsync(Track anchor, int attempt, CancellationToken ct);
}

internal sealed class YoutubeRecommendations(YtDlpClient ytdlp, ILogger<YoutubeRecommendations> logger) : IYoutubeRecommendations
{
    public async Task<IReadOnlyList<AutoplayCandidate>> GetAsync(Track seed, int limit, CancellationToken ct)
    {
        var anchor = seed.Source == TrackSource.Youtube ? seed : await FindYoutubeRecordingAsync(seed, ct);
        if (anchor is not null)
        {
            var mix = await TryMixAsync(anchor, limit, ct);
            if (mix.Length > 0) return mix;
        }

        anchor ??= seed;
        var related = await SearchAsync(anchor.Title, limit, ct);
        var series = Collect(related, anchor, AutoplaySource.Series, track => AutoplaySelection.SameSeries(anchor, track));
        return series.Length > 0 || limit < 75 ? series : Collect(related, anchor, AutoplaySource.Search);
    }

    private async Task<Track?> FindYoutubeRecordingAsync(Track seed, CancellationToken ct)
    {
        var query = string.Join(" ", seed.CreditedArtists.Append(seed.SongTitle ?? seed.Title));
        try
        {
            var matches = await SearchAsync(query, 5, ct);
            return matches.FirstOrDefault(track => AutoplaySelection.MatchesRecording(seed, track));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Could not find a YouTube recording for '{TrackId}'. Trying a broader search.", seed.Id);
            return null;
        }
    }

    private async Task<AutoplayCandidate[]> TryMixAsync(Track anchor, int limit, CancellationToken ct)
    {
        var id = Uri.EscapeDataString(anchor.Id);
        try
        {
            var tracks = await ReadEntriesAsync($"https://www.youtube.com/watch?v={id}&list=RD{id}", limit, ct);
            return Collect(tracks, anchor, AutoplaySource.Mix);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "YouTube Mix for '{VideoId}' failed. Trying another source.", anchor.Id);
            return [];
        }
    }

    public async Task<IReadOnlyList<AutoplayCandidate>> ExploreAsync(Track anchor, int attempt, CancellationToken ct)
    {
        var phase = attempt % 4;
        if (phase == 0 && anchor.Source == TrackSource.Youtube)
        {
            var mix = await TryMixAsync(anchor, 25, ct);
            if (mix.Length > 0) return mix;
        }

        var title = Regex.Replace(anchor.SongTitle ?? anchor.Title, @"[_|]+", " ");
        title = Regex.Replace(title, @"\b(vol(?:ume)?|part|episode)\s*\d+\b", "", RegexOptions.IgnoreCase).Trim();
        if (title.Length == 0) title = anchor.Title;
        var artists = string.Join(" ", anchor.CreditedArtists);
        var subject = string.Join(" ", title.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3));
        var query = phase switch
        {
            0 => title,
            1 when artists.Length > 0 => artists + " music",
            1 => subject + " music",
            2 => title + " music",
            _ => subject + " mix",
        };
        var count = Math.Min(200, 25 + attempt / 4 * 25);
        return Collect(await SearchAsync(query, count, ct), anchor, AutoplaySource.Search);
    }

    public async Task<Track?> ResolveAsync(Track candidate, CancellationToken ct)
    {
        var json = await ytdlp.RunAsync(["--dump-single-json", "--no-playlist", "--no-warnings", "--", candidate.SourceUrl], ct);
        return JsonSerializer.Deserialize<YoutubeMetadata>(json) is { } metadata && IsCandidate(metadata)
            ? YoutubeTrackResolver.ParseTrack(metadata) : null;
    }

    private Task<Track[]> SearchAsync(string query, int limit, CancellationToken ct)
        => ReadEntriesAsync($"ytsearch{limit}:{query}", limit, ct);

    private async Task<Track[]> ReadEntriesAsync(string input, int limit, CancellationToken ct)
    {
        var json = await ytdlp.RunAsync(
            ["--dump-single-json", "--flat-playlist", "--playlist-end", limit.ToString(), "--no-warnings", "--", input], ct);
        return (JsonSerializer.Deserialize<YoutubeMetadata>(json)?.Entries ?? [])
            .Where(IsCandidate).Select(YoutubeTrackResolver.ParseTrack).ToArray();
    }

    private static bool IsCandidate(YoutubeMetadata? metadata)
        => metadata is not null && !string.IsNullOrWhiteSpace(metadata.Id) && !string.IsNullOrWhiteSpace(metadata.Title)
            && metadata.IsLive != true && metadata.LiveStatus is not ("is_live" or "is_upcoming")
            && metadata.Availability is not ("private" or "premium_only" or "subscriber_only" or "needs_auth");

    private static AutoplayCandidate[] Collect(Track[] tracks, Track anchor, AutoplaySource source, Func<Track, bool>? matches = null)
        => tracks.Select((track, rank) => new AutoplayCandidate(track, source, rank))
            .Where(candidate => !AutoplaySelection.SameSong(anchor, candidate.Track)
                && AutoplaySelection.Suitable(anchor, candidate.Track, allowUnknownDuration: true)
                && (matches is null || matches(candidate.Track)))
            .ToArray();
}
