using Application.Discord.Voice;
using Domain.ValueObjects;
using Modules.Music.Application.Playback;
using System.Collections.Concurrent;

namespace Modules.Music.Application.Voice;

internal sealed class PlayerVoiceCoordinator(
    IVoiceSessionManager voiceSessionManager,
    PlaybackCommandQueue playbackCommandQueue)
{
    private readonly ConcurrentDictionary<GuildId, SemaphoreSlim> _guildLocks = new();

    public async Task JoinAsync(GuildId guildId, ChannelId voiceChannelId, CancellationToken ct)
    {
        var gate = _guildLocks.GetOrAdd(guildId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await voiceSessionManager.RequestJoinAsync(guildId, voiceChannelId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task LeaveAsync(GuildId guildId, CancellationToken ct)
    {
        var gate = _guildLocks.GetOrAdd(guildId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await playbackCommandQueue.StopAsync(guildId, ct);
            await voiceSessionManager.RequestLeaveAsync(guildId, ct);
        }
        finally
        {
            gate.Release();
        }
    }
}
