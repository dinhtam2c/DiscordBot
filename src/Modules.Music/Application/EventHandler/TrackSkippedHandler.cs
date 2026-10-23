using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class TrackSkippedHandler(PlaybackCommandQueue playbackCommandQueue)
    : IEventHandler<TrackSkippedEvent>
{
    public Task HandleAsync(TrackSkippedEvent @event, CancellationToken ct)
    {
        playbackCommandQueue.EnqueueStop(@event.guildId);
        return Task.CompletedTask;
    }
}
