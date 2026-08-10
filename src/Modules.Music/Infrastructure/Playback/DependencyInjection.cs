using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Infrastructure.Playback.NativePlayback;

namespace Modules.Music.Infrastructure.Playback;

internal static class DependencyInjection
{
    public static IServiceCollection AddPlayback(this IServiceCollection services)
    {
        services.AddNativePlayback();
        return services;
    }
}
