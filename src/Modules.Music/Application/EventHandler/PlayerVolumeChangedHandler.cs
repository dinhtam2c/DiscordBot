using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlayerVolumeChangedHandler(PlaybackCommandQueue commandQueue)
    : IEventHandler<PlayerVolumeChangedEvent>
{
    public Task HandleAsync(PlayerVolumeChangedEvent @event, CancellationToken ct)
    {
        commandQueue.EnqueueSetVolume(@event.GuildId, @event.Volume);
        return Task.CompletedTask;
    }
}
