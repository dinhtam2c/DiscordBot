using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.Repositories;
using Modules.Music.Infrastructure.AudioSource;
using Modules.Music.Infrastructure.Playback;
using Modules.Music.Infrastructure.Repositories;
using Modules.Music.Infrastructure.TrackResolver;

namespace Modules.Music.Infrastructure;

internal static class DependencyInjection
{
    internal static IServiceCollection AddMusicInfrastructure(this IServiceCollection services)
    {
        services.AddTrackResolver();
        services.AddAudioSource();
        services.AddPlayback();

        services.AddSingleton(typeof(InMemoryStore<,>));
        services.AddScoped<IPlayerRepository, MemoryPlayerRepository>();

        return services;
    }
}
