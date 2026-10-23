using Domain.ValueObjects;

namespace Application.Discord.Voice;

public interface IVoiceSessionManager
{
    Task RequestJoinAsync(GuildId guildId, ChannelId voiceChannelId, CancellationToken ct);

    Task RequestLeaveAsync(GuildId guildId, CancellationToken ct);
}
