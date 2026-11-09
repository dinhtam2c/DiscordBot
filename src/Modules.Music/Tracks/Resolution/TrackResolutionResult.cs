namespace Modules.Music.Tracks.Resolution;

internal sealed record TrackResolutionResult(IReadOnlyList<Track> Tracks, PlaylistInfo? Playlist = null);

internal sealed record PlaylistInfo(
    string Title,
    string SourceUrl,
    string? CreatorName,
    string? CreatorUrl);
