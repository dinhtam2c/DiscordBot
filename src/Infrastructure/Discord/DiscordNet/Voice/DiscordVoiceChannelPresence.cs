using System.Collections.Concurrent;
using Application.Discord.Voice;
using Discord;
using Discord.WebSocket;
using Domain.ValueObjects;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Discord.DiscordNet.Voice;

internal sealed class DiscordVoiceChannelPresence(DiscordSocketClient client) : IVoiceChannelPresence, IHostedService
{
    private readonly ConcurrentDictionary<GuildId, Dictionary<ChannelId, bool>> _snapshots = new();
    private bool _needsRefresh = true;

    public event Action<GuildId>? Changed;

    public bool? HasListeners(GuildId guildId, ChannelId channelId)
        => client.ConnectionState == ConnectionState.Connected
            && _snapshots.TryGetValue(guildId, out var channels)
            && channels.TryGetValue(channelId, out var present) ? present : null;

    public Task StartAsync(CancellationToken ct)
    {
        client.UserVoiceStateUpdated += OnVoiceStateAsync;
        client.GuildAvailable += OnGuildAvailableAsync;
        client.GuildUnavailable += OnGuildUnavailableAsync;
        client.Ready += RefreshAsync;
        client.Disconnected += OnDisconnectedAsync;
        client.LatencyUpdated += OnLatencyAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        client.UserVoiceStateUpdated -= OnVoiceStateAsync;
        client.GuildAvailable -= OnGuildAvailableAsync;
        client.GuildUnavailable -= OnGuildUnavailableAsync;
        client.Ready -= RefreshAsync;
        client.Disconnected -= OnDisconnectedAsync;
        client.LatencyUpdated -= OnLatencyAsync;
        _snapshots.Clear();
        return Task.CompletedTask;
    }

    private void Capture(SocketGuild guild)
    {
        if (client.ConnectionState != ConnectionState.Connected || !((IGuild)guild).Available)
            return;
        _snapshots[new GuildId(guild.Id)] = guild.VoiceChannels.ToDictionary(
            channel => new ChannelId(channel.Id), channel => channel.ConnectedUsers.Any(user => !user.IsBot));
        Changed?.Invoke(new GuildId(guild.Id));
    }

    private Task OnVoiceStateAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (before.VoiceChannel?.Id != after.VoiceChannel?.Id && user is SocketGuildUser member)
            Capture(member.Guild);
        return Task.CompletedTask;
    }

    private Task OnGuildAvailableAsync(SocketGuild guild) { Capture(guild); return Task.CompletedTask; }
    private Task OnGuildUnavailableAsync(SocketGuild guild)
    {
        _snapshots.TryRemove(new GuildId(guild.Id), out _);
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(Exception exception)
    {
        _snapshots.Clear();
        _needsRefresh = true;
        return Task.CompletedTask;
    }

    private Task RefreshAsync()
    {
        foreach (var guild in client.Guilds) Capture(guild);
        _needsRefresh = false;
        return Task.CompletedTask;
    }

    // A resumed gateway need not raise Ready/GuildAvailable again. Refresh once
    // after its first successful heartbeat; ordinary heartbeats do no work.
    private Task OnLatencyAsync(int previous, int current)
        => _needsRefresh && client.ConnectionState == ConnectionState.Connected ? RefreshAsync() : Task.CompletedTask;
}
