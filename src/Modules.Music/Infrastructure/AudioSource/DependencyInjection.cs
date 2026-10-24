using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.AudioSource;
using Modules.Music.Infrastructure.AudioSource.SoundCloud;

namespace Modules.Music.Infrastructure.AudioSource;

internal static class DependencyInjection
{
    public static IServiceCollection AddAudioSource(this IServiceCollection services)
    {
        services.AddSingleton<IAudioSource, YoutubeAudioSource>();
        services.AddSingleton<IAudioSource, SoundCloudAudioSource>();

        services.AddSingleton<IAudioSourceResolver, AudioSourceResolver>();
        return services;
    }
}
