using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlaybackPausedHandler(PlaybackCommandQueue commandQueue)
    : IEventHandler<PlaybackPausedEvent>
{
    public Task HandleAsync(PlaybackPausedEvent @event, CancellationToken ct)
    {
        commandQueue.EnqueuePause(@event.GuildId);
        return Task.CompletedTask;
    }
}
