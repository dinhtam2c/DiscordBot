using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlaybackStartedHandler(PlaybackCommandQueue playbackCommandQueue)
    : IEventHandler<PlaybackStartedEvent>
{
    public Task HandleAsync(PlaybackStartedEvent @event, CancellationToken ct)
    {
        playbackCommandQueue.EnqueueStart(@event.GuildId, @event.Track);
        return Task.CompletedTask;
    }
}
