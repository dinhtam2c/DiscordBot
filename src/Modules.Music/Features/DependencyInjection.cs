using Application.Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modules.Music.Features.Playback;
using Modules.Music.Features.Notifications;
using Modules.Music.Features.Autoplay;
using Modules.Music.Features.History;
using Modules.Music.Features.Queue;
using Modules.Music.Features.Search;
using Modules.Music.Features.Shortcuts;
using Modules.Music.Features.Voice;
using Modules.Music.Players;

namespace Modules.Music.Features;

internal static class DependencyInjection
{
    internal static IServiceCollection AddMusicFeatures(this IServiceCollection services)
    {
        services.AddOptions<PlaybackHistoryOptions>()
            .BindConfiguration(PlaybackHistoryOptions.SectionName)
            .Validate(options => options.Capacity > 0, "Music:History:Capacity must be positive.")
            .Validate(options => options.AutoplayWindow > 0 && options.AutoplayWindow <= options.Capacity,
                "Music:History:AutoplayWindow must be positive and no greater than Capacity.")
            .ValidateOnStart();
        services.AddScoped<EnqueueTracksUseCase>();
        services.AddScoped<SeekPlaybackUseCase>();
        services.AddSingleton<SearchResults>();
        services.AddHostedService<VoicePresenceObserver>();
        services.AddSingleton<MusicNotifications>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MusicNotifications>());
        services.AddOptions<MusicShortcutOptions>()
            .BindConfiguration(MusicShortcutOptions.SectionName)
            .Validate(options => options.Shortcuts is not null && options.Shortcuts.All(entry =>
                    entry.Value is not null
                    && !string.IsNullOrWhiteSpace(entry.Value.Description)
                    && !string.IsNullOrWhiteSpace(entry.Value.Query)
                    && Enum.IsDefined(entry.Value.Mode)),
                "Each music shortcut must have a description, a query, and a valid enqueue mode.")
            .ValidateOnStart();
        services.AddSingleton<MusicShortcutCatalog>();
        services.Configure<DiscordCommandOptions>(options =>
        {
            options.Modules.AddRange([typeof(PlaybackCommands), typeof(QueueCommands), typeof(VoiceCommands), typeof(SearchCommands), typeof(AutoplayCommands), typeof(HistoryCommands)]);
            options.ConfigureModules.Add(ShortcutCommands.RegisterAsync);
        });
        return services;
    }
}
