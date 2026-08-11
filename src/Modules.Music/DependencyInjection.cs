using Application;
using Application.Event;
using Microsoft.Extensions.DependencyInjection;
using Modules.Music.Application.Commands.Join;
using Modules.Music.Application.Commands.Laviai;
using Modules.Music.Application.Commands.Leave;
using Modules.Music.Application.Commands.Loop;
using Modules.Music.Application.Commands.NowPlaying;
using Modules.Music.Application.Commands.Play;
using Modules.Music.Application.Commands.Queue;
using Modules.Music.Application.Commands.Remove;
using Modules.Music.Application.Commands.Skip;
using Modules.Music.Application.Commands.Stop;
using Modules.Music.Application.Commands.Tungbeo;
using Modules.Music.Application.EventHandler;
using Modules.Music.Application.Playback;
using Modules.Music.Application.Repositories;
using Modules.Music.Domain.Players.Events;
using Modules.Music.Infrastructure.AudioSource;
using Modules.Music.Infrastructure.Playback;
using Modules.Music.Infrastructure.Repositories;
using Modules.Music.Infrastructure.TrackResolver;

namespace Modules.Music;

public static class DependencyInjection
{
    public static IServiceCollection AddMusicModule(this IServiceCollection services)
    {
        services.AddTrackResolver();
        services.AddAudioSource();
        services.AddPlayback();

        services.AddSingleton<PlayerStore>();
        services.AddScoped<IPlayerRepository, MemoryPlayerRepository>();

        services.AddSingleton<PlaybackController>();

        services.AddEventHandlers<PlayerJoinedVoiceChannelEvent, PlayerJoinedVoiceChannelHandler>();
        services.AddEventHandlers<PlayerLeftVoiceChannelEvent, PlayerLeftVoiceChannelHandler>();
        services.AddEventHandlers<PlaybackStartedEvent, PlaybackStartedHandler>();
        services.AddEventHandlers<TrackSkippedEvent, TrackSkippedHandler>();
        services.AddEventHandlers<PlaybackStoppedEvent, PlaybackStoppedHandler>();

        services.AddInteraction<JoinCommand, JoinHandler>();
        services.AddInteraction<LeaveCommand, LeaveHandler>();
        services.AddInteraction<PlayCommand, PlayHandler>();
        services.AddInteraction<SkipCommand, SkipHandler>();
        services.AddInteraction<LoopCommand, LoopHandler>();
        services.AddInteraction<StopCommand, StopHandler>();
        services.AddInteraction<RemoveCommand, RemoveHandler>();

        //troll
        services.AddInteraction<LaviaiCommand, LaviaiHandler>();
        services.AddInteraction<TungbeoCommand, TungbeoHandler>();

        services.AddInteraction<NowPlayingCommand, NowPlayingHandler>();
        services.AddInteraction<QueueCommand, QueueHandler>();

        return services;
    }
}
