using Application.Event;
using Modules.Music.Application.Voice;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlayerJoinedVoiceChannelHandler(PlayerVoiceCoordinator voiceCoordinator)
    : IEventHandler<PlayerJoinedVoiceChannelEvent>
{
    public Task HandleAsync(PlayerJoinedVoiceChannelEvent @event, CancellationToken ct)
        => voiceCoordinator.JoinAsync(@event.GuildId, @event.VoiceChannelId, ct);
}
