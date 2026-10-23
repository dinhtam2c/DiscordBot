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
    IServiceScopeFactory scopeFactory,
    ILogger<PlaybackController> logger)
{
    private readonly ConcurrentDictionary<GuildId, PlaybackSession> _sessions = new();

    public async Task StartAsync(GuildId guildId, Track track, IVoiceSession voiceSession)
    {
        var session = new PlaybackSession(playbackFactory, voiceSession, track, appLifetime.Stopping);

        if (!_sessions.TryAdd(guildId, session))
            throw new InvalidOperationException($"A playback session is already running for guild {guildId}.");

        try
        {
            logger.LogInformation("Starting playback session for guild {GuildId}, Track: {TrackTitle}", guildId, track.Title);
            session.Start();
            _ = ObserveSessionAsync(guildId, session);
        }
        catch
        {
            RemoveIfCurrent(guildId, session);
            await session.DisposeAsync();
            throw;
        }
    }

    private async Task ObserveSessionAsync(GuildId guildId, PlaybackSession session)
    {
        var shouldAdvanceQueue = false;

        try
        {
            await session.Completion;
            shouldAdvanceQueue = true;
        }
        catch (OperationCanceledException) when (appLifetime.Stopping.IsCancellationRequested)
        {
            logger.LogInformation("Playback session for guild {GuildId} was cancelled during shutdown.", guildId);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Playback session for guild {GuildId} was stopped.", guildId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Playback failed for guild {GuildId}.", guildId);
            shouldAdvanceQueue = true;
        }
        finally
        {
            if (RemoveIfCurrent(guildId, session))
            {
                try
                {
                    await session.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to dispose playback session for guild {GuildId}.", guildId);
                }

                if (shouldAdvanceQueue && !appLifetime.Stopping.IsCancellationRequested)
                    await AdvanceQueueAsync(guildId);
            }
        }
    }

    private bool RemoveIfCurrent(GuildId guildId, PlaybackSession session)
        => ((ICollection<KeyValuePair<GuildId, PlaybackSession>>)_sessions)
            .Remove(new KeyValuePair<GuildId, PlaybackSession>(guildId, session));

    private async Task AdvanceQueueAsync(GuildId guildId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var progressor = scope.ServiceProvider.GetRequiredService<PlaybackQueueProgressor>();
            await progressor.TrackFinishedAsync(guildId, appLifetime.Stopping);
        }
        catch (OperationCanceledException) when (appLifetime.Stopping.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to advance playback queue for guild {GuildId}.", guildId);
        }
    }

    public async Task StopCurrentAsync(GuildId guildId)
    {
        if (!_sessions.TryGetValue(guildId, out var session) || !RemoveIfCurrent(guildId, session))
            return;

        await session.DisposeAsync();
    }

    public Task StopAsync(GuildId guildId) => StopCurrentAsync(guildId);
}
