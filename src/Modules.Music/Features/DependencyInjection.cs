using Application;
using Application.Discord.Interactions.Command;
using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Features.Clear;
using Modules.Music.Features.Join;
using Modules.Music.Features.Leave;
using Modules.Music.Features.Loop;
using Modules.Music.Features.Move;
using Modules.Music.Features.NowPlaying;
using Modules.Music.Features.Pause;
using Modules.Music.Features.Play;
using Modules.Music.Features.PlayNext;
using Modules.Music.Features.Queue;
using Modules.Music.Features.Remove;
using Modules.Music.Features.Resume;
using Modules.Music.Features.Shuffle;
using Modules.Music.Features.Skip;
using Modules.Music.Features.Stop;
using Modules.Music.Features.Volume;
using Modules.Music.Features.Shortcuts;
using Modules.Music.Features.EnqueueTracks;

namespace Modules.Music.Features;

internal static class DependencyInjection
{
    internal static IServiceCollection AddMusicFeatures(this IServiceCollection services)
    {
        services.AddScoped<EnqueueTracksUseCase>();
        services.AddOptions<MusicShortcutOptions>()
            .BindConfiguration(MusicShortcutOptions.SectionName)
            .Validate(options => options.Shortcuts is not null && options.Shortcuts.All(entry =>
                    entry.Value is not null
                    && !string.IsNullOrWhiteSpace(entry.Value.Description)
                    && !string.IsNullOrWhiteSpace(entry.Value.Query)
                    && Enum.IsDefined(entry.Value.Mode)),
                "Every music shortcut needs a description, query, and valid mode.")
            .ValidateOnStart();
        services.AddSingleton<MusicShortcutCatalog>();
        services.AddSingleton<IInteractionCommandSource>(sp => sp.GetRequiredService<MusicShortcutCatalog>());
        services.AddScoped<ShortcutInteractionHandler>();

        services.AddInteraction<JoinCommand, JoinHandler>();
        services.AddInteraction<LeaveCommand, LeaveHandler>();
        services.AddInteraction<PlayCommand, PlayHandler>();
        services.AddInteraction<PlayNextCommand, PlayNextHandler>();
        services.AddInteraction<PauseCommand, PauseHandler>();
        services.AddInteraction<ResumeCommand, ResumeHandler>();
        services.AddInteraction<SkipCommand, SkipHandler>();
        services.AddInteraction<NowPlayingCommand, NowPlayingHandler>();
        services.AddInteraction<QueueCommand, QueueHandler>();
        services.AddInteraction<LoopCommand, LoopHandler>();
        services.AddInteraction<StopCommand, StopHandler>();
        services.AddInteraction<RemoveCommand, RemoveHandler>();
        services.AddInteraction<MoveCommand, MoveHandler>();
        services.AddInteraction<ShuffleCommand, ShuffleHandler>();
        services.AddInteraction<ClearCommand, ClearHandler>();
        services.AddInteraction<VolumeCommand, VolumeHandler>();

        return services;
    }
}
