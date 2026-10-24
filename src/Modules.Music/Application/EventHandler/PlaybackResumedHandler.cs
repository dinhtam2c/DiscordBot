using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlaybackResumedHandler(PlaybackCommandQueue commandQueue)
    : IEventHandler<PlaybackResumedEvent>
{
    public Task HandleAsync(PlaybackResumedEvent @event, CancellationToken ct)
    {
        commandQueue.EnqueueResume(@event.GuildId);
        return Task.CompletedTask;
    }
}
