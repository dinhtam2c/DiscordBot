using Modules.Music.Domain.Players;

namespace Modules.Music.Application.TrackResolver;

internal interface ITrackResolver
{
    TrackSource Source { get; }

    bool CanResolveUrl(Uri uri);

    Task<IReadOnlyList<Track>> ResolveAsync(string query, CancellationToken ct = default);

    Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken ct = default);
}
