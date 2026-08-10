using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class TrackSkippedHandler(PlaybackController playbackController) : IEventHandler<TrackSkippedEvent>
{
    public async Task HandleAsync(TrackSkippedEvent @event, CancellationToken ct)
    {
        await playbackController.StopCurrentAsync(@event.guildId, ct);
    }
}
