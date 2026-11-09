using Application.Abstractions;
using Infrastructure.Clock;
using Infrastructure.Discord;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddDiscord();
        services.AddPersistence();

        services.AddSingleton<IAppLifetime, AppLifetime>();

        return services;
    }
}
