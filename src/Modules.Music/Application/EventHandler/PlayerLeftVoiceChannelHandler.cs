using Application.Discord.Voice;
using Application.Event;
using Modules.Music.Application.Playback;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlayerLeftVoiceChannelHandler(
    IVoiceSessionManager voiceSessionManager,
    PlaybackController playbackController) : IEventHandler<PlayerLeftVoiceChannelEvent>
{
    public async Task HandleAsync(PlayerLeftVoiceChannelEvent @event, CancellationToken ct)
    {
        await playbackController.StopAsync(@event.guildId, ct);
        await voiceSessionManager.LeaveAsync(@event.guildId, ct);
    }
}
