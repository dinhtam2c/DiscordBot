using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlaybackStoppedHandler(PlaybackController playbackController) : IEventHandler<PlaybackStoppedEvent>
{
    public async Task HandleAsync(PlaybackStoppedEvent @event, CancellationToken ct)
    {
        await playbackController.StopAsync(@event.guildId, ct);
    }
}
