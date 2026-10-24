using Application;
using Application.Discord.Interactions.Command;
using Application.Event;
using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.Commands.Clear;
using Modules.Music.Application.Commands.Join;
using Modules.Music.Application.Commands.Leave;
using Modules.Music.Application.Commands.Loop;
using Modules.Music.Application.Commands.Move;
using Modules.Music.Application.Commands.NowPlaying;
using Modules.Music.Application.Commands.Pause;
using Modules.Music.Application.Commands.Play;
using Modules.Music.Application.Commands.PlayNext;
using Modules.Music.Application.Commands.Queue;
using Modules.Music.Application.Commands.Remove;
using Modules.Music.Application.Commands.Resume;
using Modules.Music.Application.Commands.Shuffle;
using Modules.Music.Application.Commands.Skip;
using Modules.Music.Application.Commands.Stop;
using Modules.Music.Application.Commands.Volume;
using Modules.Music.Application.EventHandler;
using Modules.Music.Application.Playback;
using Modules.Music.Application.Shortcuts;
using Modules.Music.Application.TrackResolver;
using Modules.Music.Application.Voice;
using Modules.Music.Application.UseCases.EnqueueTracks;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application;

internal static class DependencyInjection
{
    internal static IServiceCollection AddMusicApplication(this IServiceCollection services)
    {
        services.AddSingleton<PlaybackCommandQueue>();
        services.AddSingleton<PlaybackWorker>();
        services.AddSingleton<PlayerVoiceCoordinator>();
        services.AddScoped<EnqueueTracksUseCase>();
        services.AddScoped<PlaybackQueueProgressor>();
        services.AddOptions<TrackSearchOptions>()
            .BindConfiguration(TrackSearchOptions.SectionName)
            .Validate(options => options.SearchResultCount > 0, "Track search result count must be positive.")
            .ValidateOnStart();
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

        services.AddEventHandlers<PlayerJoinedVoiceChannelEvent, PlayerJoinedVoiceChannelHandler>();
        services.AddEventHandlers<PlayerLeftVoiceChannelEvent, PlayerLeftVoiceChannelHandler>();
        services.AddEventHandlers<PlayerVolumeChangedEvent, PlayerVolumeChangedHandler>();
        services.AddEventHandlers<PlaybackPausedEvent, PlaybackPausedHandler>();
        services.AddEventHandlers<PlaybackResumedEvent, PlaybackResumedHandler>();
        services.AddEventHandlers<PlaybackStartedEvent, PlaybackStartedHandler>();
        services.AddEventHandlers<TrackSkippedEvent, TrackSkippedHandler>();
        services.AddEventHandlers<PlaybackStoppedEvent, PlaybackStoppedHandler>();

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
