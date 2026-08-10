using Application.Discord.Voice;
using Application.Event;
using Microsoft.Extensions.Logging;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Application.EventHandler;

internal sealed class PlayerJoinedVoiceChannelHandler(
    IVoiceSessionManager voiceSessionManager,
    ILogger<PlayerJoinedVoiceChannelHandler> logger) : IEventHandler<PlayerJoinedVoiceChannelEvent>
{
    public async Task HandleAsync(PlayerJoinedVoiceChannelEvent @event, CancellationToken ct)
    {
        await voiceSessionManager.JoinAsync(@event.GuildId, @event.VoiceChannelId, ct);
        logger.LogInformation("Player joined voice channel {VoiceChannelId} in guild {GuildId}", @event.VoiceChannelId.Value, @event.GuildId.Value);
    }
}
