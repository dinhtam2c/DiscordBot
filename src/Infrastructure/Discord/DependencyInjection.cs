using Application.Discord.User;
using Application.Discord.Messages;
using Application.Discord.Voice;
using Discord;
using Discord.Interactions;
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

        services.AddSingleton(sp => new InteractionService(
            sp.GetRequiredService<DiscordSocketClient>().Rest,
            new InteractionServiceConfig { DefaultRunMode = RunMode.Sync, AutoServiceScopes = false }));
        services.AddSingleton<DiscordInteractionAdapter>();
        services.AddSingleton<DiscordCommandRegistrar>();
        services.AddSingleton<DiscordVoiceChannelPresence>();
        services.AddSingleton<IVoiceChannelPresence>(sp => sp.GetRequiredService<DiscordVoiceChannelPresence>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DiscordVoiceChannelPresence>());
        // Register before voice and module workers so the gateway stops after their cleanup.
        services.AddHostedService<DiscordGateway>();
        services.AddSingleton<DiscordMessageQueue>();
        services.AddHostedService<DiscordMessageWorker>();

        services.AddSingleton<IDiscordUserService, DiscordUserService>();

        services.AddSingleton<DiscordVoiceSessionManager>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<DiscordVoiceSessionManager>());
        services.AddSingleton<IVoiceSessionManager>(sp => sp.GetRequiredService<DiscordVoiceSessionManager>());
        services.AddSingleton<IVoiceOutputProvider>(sp => sp.GetRequiredService<DiscordVoiceSessionManager>());

        return services;
    }
}
