using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlayerLeftVoiceChannelHandler(PlaybackCommandQueue playbackCommandQueue)
    : IEventHandler<PlayerLeftVoiceChannelEvent>
{
    public Task HandleAsync(PlayerLeftVoiceChannelEvent @event, CancellationToken ct)
    {
        playbackCommandQueue.EnqueueStopAndLeave(@event.guildId);
        return Task.CompletedTask;
    }
}
