using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application;
using Modules.Music.Infrastructure;

namespace Modules.Music;

public static class DependencyInjection
{
    public static IServiceCollection AddMusicModule(this IServiceCollection services)
    {
        services.AddMusicApplication();
        services.AddMusicInfrastructure();

        return services;
    }
}
