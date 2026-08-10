using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.AudioSource;

namespace Modules.Music.Infrastructure.AudioSource.Youtube;

internal static class DependencyInjection
{
    public static IServiceCollection AddYoutubeAudioSource(this IServiceCollection services)
    {
        services
            .AddOptions<YoutubeOptions>()
            .BindConfiguration(YoutubeOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAudioSource, YoutubeAudioSource>();
        return services;
    }
}
