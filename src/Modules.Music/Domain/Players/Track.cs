namespace Modules.Music.Domain.Players;

internal sealed record Track(
    string Id,
    string Title,
    string Description,
    string Artist,
    TimeSpan Duration,
    TrackSource Source,
    string SourceUrl,
    string ThumbnailUrl);
