using Infrastructure.Clock;
using Infrastructure.Discord;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddClock();
        services.AddDiscord();
        services.AddPersistence();

        return services;
    }
}
