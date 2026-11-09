namespace Modules.Music.Tracks.Resolution;

internal interface ITrackResolver
{
    TrackSource Source { get; }

    bool CanResolveUrl(Uri uri);

    Task<TrackResolutionResult> ResolveAsync(string query, CancellationToken ct = default);

    Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken ct = default);
}
