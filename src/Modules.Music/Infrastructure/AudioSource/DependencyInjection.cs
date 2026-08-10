using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.AudioSource;
using Modules.Music.Infrastructure.AudioSource.Youtube;

namespace Modules.Music.Infrastructure.AudioSource;

internal static class DependencyInjection
{
    public static IServiceCollection AddAudioSource(this IServiceCollection services)
    {
        services.AddYoutubeAudioSource();

        services.AddSingleton<IAudioSourceResolver, AudioSourceResolver>();
        return services;
    }
}
