namespace Modules.Music.Tracks;

internal sealed record Track(
    string Id,
    string Title,
    string Description,
    string Artist,
    TimeSpan Duration,
    TrackSource Source,
    string SourceUrl,
    string ThumbnailUrl,
    string? UploaderName = null,
    string? UploaderUrl = null);
