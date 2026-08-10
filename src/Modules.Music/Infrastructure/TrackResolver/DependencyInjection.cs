using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.TrackResolver;

namespace Modules.Music.Infrastructure.TrackResolver;

internal static class DependencyInjection
{
    public static IServiceCollection AddTrackResolver(this IServiceCollection services)
    {
        services.AddSingleton<ITrackResolverFactory, TrackResolverFactory>();
     
        services.AddSingleton<ITrackResolver, YoutubeTrackResolver>();

        return services;
    }
}
