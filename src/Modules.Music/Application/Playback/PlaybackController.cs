using Application.Abstractions;
using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Music.Domain.Players;
using System.Collections.Concurrent;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackController(
    IPlaybackFactory playbackFactory,
    IAppLifetime appLifetime,
    IServiceProvider serviceProvider,
    ILogger<PlaybackController> logger)
{
    private readonly ConcurrentDictionary<GuildId, PlaybackSession> _sessions = [];

    public async Task StartAsync(Player player, IVoiceSession voiceSession)
    {
        var lg = serviceProvider.GetRequiredService<ILogger<PlaybackSession>>();
        var session = new PlaybackSession(playbackFactory, voiceSession, player, lg, appLifetime.Stopping);

        if (!_sessions.TryAdd(player.GuildId, session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException($"A playback session is already running for guild {player.GuildId}.");
        }

        try
        {
            logger.LogInformation("Starting playback session for guild {GuildId}.", player.GuildId);

            session.Start();
            _ = ObserveSession(player.GuildId, session);
        }
        catch
        {
            _sessions.TryRemove(player.GuildId, out _);
            await session.DisposeAsync();
            throw;
        }
    }

    private async Task ObserveSession(GuildId guildId, PlaybackSession session)
    {
        try
        {
            await session.Completion;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Playback session for guild {GuildId} was cancelled.", guildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playback session for guild {GuildId} terminated with an error.", guildId);
        }
        finally
        {
            if (_sessions.TryRemove(guildId, out _))
            {
                logger.LogInformation("Playback session for guild {GuildId} has ended.", guildId);
                await session.DisposeAsync();
            }
        }
    }

    public async Task StopCurrentAsync(GuildId guildId)
    {
        if (!_sessions.TryGetValue(guildId, out var session))
            return;

        await session.StopCurrentPlaybackAsync();
    }

    public async Task StopAsync(GuildId guildId)
    {
        if (!_sessions.TryRemove(guildId, out var session))
            return;

        await session.DisposeAsync();
    }
}
