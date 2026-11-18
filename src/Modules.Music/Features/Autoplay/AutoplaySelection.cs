using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Modules.Music.Players;
using Modules.Music.Tracks;

namespace Modules.Music.Features.Autoplay;

internal enum AutoplaySource { Mix, Series, Search }

internal sealed record AutoplayCandidate(Track Track, AutoplaySource Source, int Rank);

internal sealed class AutoplaySelection(Track seed, IReadOnlyList<PlaybackHistoryEntry> history)
{
    private static readonly Regex Words = new(@"[\p{L}\p{N}]+");
    private static readonly Regex Decorations = new(@"\b(?:(?:official )?(?:music video|lyric video|audio|mv)|official video|visualizer|lyrics?|4k|hd)\b");
    private static readonly Regex Versions = new(@"\b(?:remix|mashup|cover|acoustic|sped up|slowed|nightcore|karaoke)\b");
    private static readonly Regex Compilations = new(@"\b(?:compilation|playlist|nonstop|1 hour|10 hours)\b");
    private static readonly Regex Episode = new(@"\b(?:vol(?:ume)?|part|episode)\s*(\d+)\b");

    private readonly Song[] _excluded = history.Select(entry => Describe(entry.Item.Track)).Prepend(Describe(seed)).ToArray();
    private readonly Song[] _recent = history.Where(entry => entry.Status is not PlaybackHistoryStatus.Failed)
        .Reverse().Take(5).Select(entry => Describe(entry.Item.Track)).ToArray();

    public bool IsExcluded(Track track)
    {
        var song = Describe(track);
        return _excluded.Any(previous => SameSong(previous, song));
    }

    public IEnumerable<AutoplayCandidate> Rank(IEnumerable<AutoplayCandidate> candidates)
        => candidates
            .Select(candidate => (Candidate: candidate, Song: Describe(candidate.Track)))
            .Where(item => Suitable(seed, item.Candidate.Track, allowUnknownDuration: true)
                && !_excluded.Any(previous => SameSong(previous, item.Song)))
            .DistinctBy(item => item.Candidate.Track.Id)
            .OrderByDescending(item => Score(item.Candidate, item.Song))
            .Select(item => item.Candidate);

    public double Score(AutoplayCandidate candidate) => Score(candidate, Describe(candidate.Track));

    private double Score(AutoplayCandidate candidate, Song song)
    {
        var rankScore = 1d / (1 + candidate.Rank / 12d);
        var repeats = _recent.Select((previous, index) => (previous, index))
            .Where(item => SharesArtist(item.previous, song) || SameChannel(item.previous, song)
                || ((item.previous.Channel.Length == 0 || song.Channel.Length == 0)
                    && song.Uploader.Length > 0 && item.previous.Uploader == song.Uploader))
            .ToArray();
        var repetition = repeats.Sum(item => .24 / (item.index + 1)) + .13 * Math.Max(0, repeats.Length - 1);
        var versions = song.Versions;
        var versionBonus = versions.Any(_excluded[0].Versions.Contains) ? .10 : 0;
        var versionPenalty = versions.Contains("mashup") && !_excluded[0].Versions.Contains("mashup") ? .20 : 0;
        return rankScore + versionBonus - repetition - versionPenalty;
    }

    public static bool Suitable(Track seed, Track track, bool allowUnknownDuration = false)
    {
        if (track.Duration <= TimeSpan.Zero && !allowUnknownDuration) return false;
        var title = Normalize(track.Title);
        var seedVersions = Versions.Matches(Normalize(seed.Title)).Select(match => match.Value).ToHashSet();
        if (Versions.Matches(title).Any(match => match.Value is not ("remix" or "mashup")
            && !seedVersions.Contains(match.Value))) return false;
        return seed.Duration.TotalMinutes >= 15 || (track.Duration.TotalMinutes < 15 && !Compilations.IsMatch(title));
    }

    public static bool SameSong(Track left, Track right) => SameSong(Describe(left), Describe(right));

    private static bool SameSong(Song left, Song right)
    {
        if (left.Track.Source == right.Track.Source && left.Track.Id == right.Track.Id) return true;
        if (left.Episode != right.Episode || left.Title.Length == 0 || right.Title.Length == 0) return false;

        var delta = Math.Abs((left.Track.Duration - right.Track.Duration).TotalSeconds);
        var knownDuration = left.Track.Duration > TimeSpan.Zero && right.Track.Duration > TimeSpan.Zero;
        var related = SharesArtist(left, right) || SameChannel(left, right);
        if (knownDuration && delta <= 8 && left.VideoTitle == right.VideoTitle
            && Math.Max(left.Words.Count, right.Words.Count) >= 3) return true;
        if (left.Title == right.Title)
            return related && (!knownDuration || delta <= 30)
                || knownDuration && delta <= 8 && left.Words.Count >= 3;
        return related && knownDuration && delta <= 12 && SimilarTitle(left, right) >= .85;
    }

    public static bool SameSeries(Track left, Track right)
    {
        var a = Describe(left);
        var b = Describe(right);
        return SameChannel(a, b) && (SimilarTitle(a, b) >= .6
            || (a.Episode.Length > 0 && b.Episode.Length > 0
                && Episode.Replace(a.Title, "").Trim() == Episode.Replace(b.Title, "").Trim()));
    }

    public static bool MatchesRecording(Track left, Track right)
    {
        var a = Describe(left);
        var b = Describe(right);
        return a.Episode == b.Episode && SimilarTitle(a, b) >= .65
            && left.Duration > TimeSpan.Zero && right.Duration > TimeSpan.Zero
            && Math.Abs((left.Duration - right.Duration).TotalSeconds) <= 15;
    }

    private static double SimilarTitle(Song left, Song right)
    {
        var overlap = left.Words.Count(right.Words.Contains);
        var total = left.Words.Count + right.Words.Count - overlap;
        return total == 0 ? 0 : (double)overlap / total;
    }

    private static bool SharesArtist(Song left, Song right) => left.Artists.Overlaps(right.Artists);

    private static bool SameChannel(Song left, Song right)
        => left.Channel.Length > 0 && left.Channel == right.Channel;

    private static Song Describe(Track track)
    {
        var artists = track.CreditedArtists.Select(Normalize).Where(name => name.Length > 0).ToHashSet();
        var channel = track.UploaderUrl?.TrimEnd('/') ?? string.Empty;
        var uploader = Normalize(track.UploaderName ?? "");
        var rawTitle = Normalize(track.Title);
        var title = Normalize(track.SongTitle ?? track.Title);
        var dash = track.Title.IndexOf(" - ", StringComparison.Ordinal);
        if (track.SongTitle is null && dash >= 0)
        {
            var prefix = Normalize(track.Title[..dash]);
            if (artists.Contains(prefix) || prefix == uploader)
                title = Normalize(track.Title[(dash + 3)..]);
        }

        var words = Words.Matches(Decorations.Replace(title, " ")).Select(match => match.Value).ToArray();
        var videoTitle = string.Join(" ", Words.Matches(Decorations.Replace(rawTitle, " ")).Select(match => match.Value));
        return new Song(track, string.Join(" ", words), videoTitle, words.ToHashSet(), artists, channel, uploader,
            string.Join(" ", Episode.Matches(rawTitle).Select(match => match.Groups[1].Value)),
            Versions.Matches(rawTitle).Select(match => match.Value).ToHashSet());
    }

    private static string Normalize(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            text.Append(ch is '\u0111' or '\u0110' ? 'd' : char.ToLowerInvariant(ch));
        }
        return string.Join(" ", Words.Matches(text.ToString()).Select(match => match.Value));
    }

    private sealed record Song(Track Track, string Title, string VideoTitle, HashSet<string> Words, HashSet<string> Artists,
        string Channel, string Uploader, string Episode, HashSet<string> Versions);
}
