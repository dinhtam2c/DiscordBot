using Domain.ValueObjects;

namespace Application.Discord.User;

public interface IDiscordUserService
{
    Task<ChannelId?> GetUserVoiceChannelIdAsync(GuildId guildId, UserId userId, CancellationToken ct);
}
