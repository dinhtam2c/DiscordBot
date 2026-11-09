using Application.Discord.User;
using Discord.WebSocket;
using Domain.ValueObjects;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordUserService(DiscordSocketClient client) : IDiscordUserService
{
    public Task<ChannelId?> GetUserVoiceChannelIdAsync(GuildId guildId, UserId userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var channelId = client.GetGuild(guildId.Value)?.GetUser(userId.Value)?.VoiceChannelId;
        return Task.FromResult(channelId is { } id ? new ChannelId(id) : (ChannelId?)null);
    }
}
