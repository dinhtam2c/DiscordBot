using Application.Discord.Channel;
using Application.Discord.User;
using Discord.WebSocket;
using Domain.ValueObjects;

namespace Infrastructure.Discord.DiscordNet;

internal sealed class DiscordUserService(DiscordSocketClient client) : IDiscordUserService
{
    public Task<DiscordUserInfo?> GetUserAsync(GuildId guildId, UserId userId, CancellationToken ct)
    {
        var guild = client.GetGuild(guildId.Value);

        if (guild is null)
        {
            return Task.FromResult<DiscordUserInfo?>(null);
        }

        var user =  guild.GetUser(userId.Value);
        if (user is null)
        {
            return Task.FromResult<DiscordUserInfo?>(null);
        }

        return Task.FromResult<DiscordUserInfo?>(new DiscordUserInfo(
            new UserId(user.Id),
            user.Username, 
            user.DisplayName, 
            user.GetDisplayAvatarUrl()));
    }


    public Task<DiscordVoiceChannelInfo?> GetUserVoiceChannelAsync(GuildId guildId, UserId userId, CancellationToken ct)
    {
        var guild = client.GetGuild(guildId.Value);
        if (guild is null)
        {
            return Task.FromResult<DiscordVoiceChannelInfo?>(null);
        }

        var user = guild.GetUser(userId.Value);
        if (user is null)
        {
            return Task.FromResult<DiscordVoiceChannelInfo?>(null);
        }

        var voiceChannel = user.VoiceChannelId;
        if (!voiceChannel.HasValue)
        {
            return Task.FromResult<DiscordVoiceChannelInfo?>(null);
        }

        var channel = guild.GetVoiceChannel(voiceChannel.Value);
        if (channel is null)
        {
            return Task.FromResult<DiscordVoiceChannelInfo?>(null);
        }

        return Task.FromResult<DiscordVoiceChannelInfo?>(new DiscordVoiceChannelInfo(
            new ChannelId(channel.Id),
            channel.Name,
            channel.Bitrate,
            channel.UserLimit,
            channel.Position,
            channel.IsNsfw,
            channel.Topic,
            channel.CreatedAt
        ));
    }
}
