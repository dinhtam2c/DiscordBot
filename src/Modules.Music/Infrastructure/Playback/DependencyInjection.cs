using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.Playback;
using Modules.Music.Infrastructure.Playback.NativePlayback;

namespace Modules.Music.Infrastructure.Playback;

internal static class DependencyInjection
{
    public static IServiceCollection AddPlayback(this IServiceCollection services)
    {
        services.AddHostedService<PlaybackHostedService>();
        services.AddNativePlayback();
        return services;
    }
}
