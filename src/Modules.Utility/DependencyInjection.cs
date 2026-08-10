using Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Utility.About;
using Modules.Utility.Echo;
using Modules.Utility.Help;
using Modules.Utility.Ping;
using Modules.Utility.UserInfo;

namespace Modules.Utility;

public static class DependencyInjection
{
    public static IServiceCollection AddUtilityModule(this IServiceCollection services)
    {
        services.AddInteraction<PingCommand, PingHandler>();
        services.AddInteraction<AboutCommand, AboutHandler>();
        services.AddInteraction<EchoCommand, EchoHandler>();
        services.AddInteraction<HelpCommand, HelpHandler>();
        services.AddInteraction<UserInfoCommand, UserInfoHandler>();

        return services;
    }
}
