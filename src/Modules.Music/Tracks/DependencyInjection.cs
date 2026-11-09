using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Tracks.AudioSources;
using Modules.Music.Tracks.Resolution;
using Modules.Music.Tracks.SoundCloud;
using Modules.Music.Tracks.Youtube;

namespace Modules.Music.Tracks;

internal static class DependencyInjection
{
    public static IServiceCollection AddTracks(this IServiceCollection services)
    {
        services.AddOptions<TrackSearchOptions>()
            .BindConfiguration(TrackSearchOptions.SectionName)
            .Validate(options => options.SearchResultCount > 0, "Track search result count must be positive.")
            .ValidateOnStart();
        services.AddSingleton<ITrackResolverFactory, TrackResolverFactory>();
        services.AddSingleton<ITrackResolver, YoutubeTrackResolver>();
        services.AddSingleton<ITrackResolver, SoundCloudTrackResolver>();
        services.AddSingleton<IAudioSourceResolver, AudioSourceResolver>();
        services.AddSingleton<IAudioSource, YoutubeAudioSource>();
        services.AddSingleton<IAudioSource, SoundCloudAudioSource>();
        return services;
    }
}
