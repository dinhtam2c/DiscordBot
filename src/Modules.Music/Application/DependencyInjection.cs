using Application;
using Application.Event;
using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.Commands.Clear;
using Modules.Music.Application.Commands.Join;
using Modules.Music.Application.Commands.Laviai;
using Modules.Music.Application.Commands.Leave;
using Modules.Music.Application.Commands.Loop;
using Modules.Music.Application.Commands.Move;
using Modules.Music.Application.Commands.NowPlaying;
using Modules.Music.Application.Commands.Play;
using Modules.Music.Application.Commands.PlayNext;
using Modules.Music.Application.Commands.Queue;
using Modules.Music.Application.Commands.Remove;
using Modules.Music.Application.Commands.Shuffle;
using Modules.Music.Application.Commands.Skip;
using Modules.Music.Application.Commands.Stop;
using Modules.Music.Application.Commands.Tungbeo;
using Modules.Music.Application.EventHandler;
using Modules.Music.Application.Playback;
using Modules.Music.Application.Voice;
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

        services.AddEventHandlers<PlayerJoinedVoiceChannelEvent, PlayerJoinedVoiceChannelHandler>();
        services.AddEventHandlers<PlayerLeftVoiceChannelEvent, PlayerLeftVoiceChannelHandler>();
        services.AddEventHandlers<PlaybackStartedEvent, PlaybackStartedHandler>();
        services.AddEventHandlers<TrackSkippedEvent, TrackSkippedHandler>();
        services.AddEventHandlers<PlaybackStoppedEvent, PlaybackStoppedHandler>();

        services.AddInteraction<JoinCommand, JoinHandler>();
        services.AddInteraction<LeaveCommand, LeaveHandler>();
        services.AddInteraction<PlayCommand, PlayHandler>();
        services.AddInteraction<PlayNextCommand, PlayNextHandler>();
        services.AddInteraction<SkipCommand, SkipHandler>();
        services.AddInteraction<NowPlayingCommand, NowPlayingHandler>();
        services.AddInteraction<QueueCommand, QueueHandler>();
        services.AddInteraction<LoopCommand, LoopHandler>();
        services.AddInteraction<StopCommand, StopHandler>();
        services.AddInteraction<RemoveCommand, RemoveHandler>();
        services.AddInteraction<MoveCommand, MoveHandler>();
        services.AddInteraction<ShuffleCommand, ShuffleHandler>();
        services.AddInteraction<ClearCommand, ClearHandler>();

        // troll
        services.AddInteraction<LaviaiCommand, LaviaiHandler>();
        services.AddInteraction<TungbeoCommand, TungbeoHandler>();

        return services;
    }
}
