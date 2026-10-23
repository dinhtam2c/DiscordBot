using Application.Event;
using Modules.Music.Application.Voice;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlayerLeftVoiceChannelHandler(PlayerVoiceCoordinator voiceCoordinator)
    : IEventHandler<PlayerLeftVoiceChannelEvent>
{
    public Task HandleAsync(PlayerLeftVoiceChannelEvent @event, CancellationToken ct)
        => voiceCoordinator.LeaveAsync(@event.guildId, ct);
}
