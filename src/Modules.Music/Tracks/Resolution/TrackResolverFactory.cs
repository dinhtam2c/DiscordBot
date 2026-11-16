namespace Modules.Music.Tracks.Resolution;

internal sealed class TrackResolverFactory(IEnumerable<ITrackResolver> resolvers) : ITrackResolverFactory
{
    private readonly Dictionary<TrackSource, ITrackResolver> _resolvers = resolvers.ToDictionary(r => r.Source);

    public ITrackResolver Select(TrackSource source) => _resolvers[source];

    public ITrackResolver Select(string query)
    {
        if (Uri.TryCreate(query.Trim(), UriKind.Absolute, out var uri))
        {
            foreach (var resolver in _resolvers.Values)
            {
                if (resolver.CanResolveUrl(uri))
                    return resolver;
            }
        }

        return _resolvers[TrackSource.Youtube];
    }
}
