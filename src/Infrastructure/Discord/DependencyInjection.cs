using Application.Discord.Runtime;
using Application.Discord.User;
using Application.Discord.Voice;
using Discord;
using Discord.WebSocket;
using Infrastructure.Discord.DiscordNet;
using Infrastructure.Discord.DiscordNet.Voice;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Discord;

public static class DependencyInjection
{
    public static IServiceCollection AddDiscord(this IServiceCollection services)
    {
        services
            .AddOptions<DiscordOptions>()
            .BindConfiguration(DiscordOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                LogGatewayIntentWarnings = false,
                AlwaysDownloadUsers = false,
            };

            return new DiscordSocketClient(config);
        });

        services.AddSingleton<IDiscordEventSubscriber, DiscordInteractionAdapter>();
        services.AddSingleton<IDiscordReadyHandler, DiscordCommandRegistrar>();

        services.AddSingleton<IDiscordRuntime, DiscordRuntime>();
        services.AddSingleton<IDiscordGateway, DiscordGateway>();

        services.AddSingleton<IDiscordUserService, DiscordUserService>();

        services.AddSingleton<DiscordVoiceSessionManager>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DiscordVoiceSessionManager>());
        services.AddSingleton<IVoiceSessionManager>(sp => sp.GetRequiredService<DiscordVoiceSessionManager>());
        services.AddSingleton<IVoiceOutputProvider>(sp => sp.GetRequiredService<DiscordVoiceSessionManager>());

        return services;
    }
}
