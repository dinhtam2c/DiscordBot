using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Features;
using Modules.Music.Playback;
using Modules.Music.Players;
using Modules.Music.Players.Coordination;
using Modules.Music.Players.Persistence;
using Modules.Music.Processes.YtDlp;
using Modules.Music.Tracks;

namespace Modules.Music;

public static class DependencyInjection
{
    public static IServiceCollection AddMusicModule(this IServiceCollection services)
    {
        services.AddMusicFeatures();
        services.AddTracks();
        services.AddYtDlp();
        services.AddPlayback();
        services.AddScoped<IPlayerRepository, MemoryPlayerRepository>();
        services.AddSingleton<PlayerCoordinator>();
        services.AddHostedService(sp => sp.GetRequiredService<PlayerCoordinator>());

        return services;
    }
}
