using Application.Discord.Channel;
using Domain.ValueObjects;

namespace Application.Discord.User;

public interface IDiscordUserService
{
    Task<DiscordUserInfo?> GetUserAsync(GuildId guildId, UserId userId, CancellationToken ct);

    Task<DiscordVoiceChannelInfo?> GetUserVoiceChannelAsync(GuildId guildId, UserId userId, CancellationToken ct);
}
