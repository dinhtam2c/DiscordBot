using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.TrackResolver;
using Modules.Music.Infrastructure.TrackResolver.SoundCloud;
using Modules.Music.Infrastructure.TrackResolver.Youtube;

namespace Modules.Music.Infrastructure.TrackResolver;

internal static class DependencyInjection
{
    public static IServiceCollection AddTrackResolver(this IServiceCollection services)
    {
        services.AddSingleton<ITrackResolverFactory, TrackResolverFactory>();
     
        services.AddSingleton<ITrackResolver, YoutubeTrackResolver>();
        services.AddSingleton<ITrackResolver, SoundCloudTrackResolver>();

        return services;
    }
}
