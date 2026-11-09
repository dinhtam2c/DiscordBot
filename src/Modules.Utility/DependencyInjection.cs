using Application.Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Utility;

public static class DependencyInjection
{
    public static IServiceCollection AddUtilityModule(this IServiceCollection services)
    {
        services.Configure<DiscordCommandOptions>(options => options.Modules.Add(typeof(UtilityCommands)));
        return services;
    }
}
