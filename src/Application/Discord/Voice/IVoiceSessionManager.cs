using Domain.ValueObjects;

namespace Application.Discord.Voice;

public interface IVoiceSessionManager
{
    Task<IVoiceSession> JoinAsync(GuildId guildId, ChannelId voiceChannelId, CancellationToken ct);

    Task LeaveAsync(GuildId guildId, CancellationToken ct);

    IVoiceSession? GetSession(GuildId guildId);
}
