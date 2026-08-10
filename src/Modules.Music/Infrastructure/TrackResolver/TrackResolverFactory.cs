using Modules.Music.Application.TrackResolver;
using Modules.Music.Domain.Players;

namespace Modules.Music.Infrastructure.TrackResolver;

internal sealed class TrackResolverFactory(IEnumerable<ITrackResolver> resolvers) : ITrackResolverFactory
{
    private readonly Dictionary<TrackSource, ITrackResolver> _resolvers = resolvers.ToDictionary(r => r.Source);

    public ITrackResolver Select(string query)
    {
        return _resolvers[TrackSource.Youtube];
    }
}
