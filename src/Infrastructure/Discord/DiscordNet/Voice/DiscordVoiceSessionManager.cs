using Application.Discord.Voice;
using Discord.WebSocket;
using Domain.ValueObjects;
using System.Collections.Concurrent;

namespace Infrastructure.Discord.DiscordNet.Voice;

internal sealed class DiscordVoiceSessionManager(DiscordSocketClient client) : IVoiceSessionManager
{
    private readonly ConcurrentDictionary<GuildId, DiscordVoiceSession> _sessions = [];

    public IVoiceSession? GetSession(GuildId guildId)
    {
        return _sessions[guildId];
    }

    public async Task<IVoiceSession> JoinAsync(GuildId guildId, ChannelId voiceChannelId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(guildId, out var existingSession))
            return existingSession;

        var guild = client.GetGuild(guildId.Value)
            ?? throw new InvalidOperationException("Guild not found");

        var voiceChannel = guild.GetVoiceChannel(voiceChannelId.Value)
            ?? throw new InvalidOperationException("Voice channel not found");

        var audioClient = await voiceChannel.ConnectAsync(selfDeaf: true);

        var session = new DiscordVoiceSession(guildId, voiceChannelId, audioClient);
        _sessions[guildId] = session;

        return session;
    }

    public async Task LeaveAsync(GuildId guildId, CancellationToken ct)
    {
        if (!_sessions.TryRemove(guildId, out var session))
            return;

        session.Dispose();

        var guild = client.GetGuild(guildId.Value);
        if (guild is null)
            return;

        var voiceChannel = guild.GetVoiceChannel(session.VoiceChannelId.Value);
        if (voiceChannel is not null)
            await voiceChannel.DisconnectAsync();
    }
}
