using System.Collections.Concurrent;
using Application.Abstractions;
using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Music.Domain.Players;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackWorker(
    PlaybackCommandQueue commandQueue,
    IPlaybackFactory playbackFactory,
    IVoiceSessionManager voiceSessionManager,
    IAppLifetime appLifetime,
    IServiceScopeFactory scopeFactory,
    ILogger<PlaybackWorker> logger)
{
    private readonly ConcurrentDictionary<GuildId, PlaybackSession> _sessions = new();

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var command in commandQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    switch (command)
                    {
                        case StartPlaybackCommand start:
                            await StartAsync(start);
                            break;
                        case StopPlaybackCommand stop:
                            await StopAsync(stop.GuildId);
                            break;
                        case StopAndLeavePlaybackCommand stopAndLeave:
                            await StopAsync(stopAndLeave.GuildId);
                            await voiceSessionManager.LeaveAsync(stopAndLeave.GuildId, appLifetime.Stopping);
                            break;
                        case PlaybackFinishedCommand finished:
                            await HandleFinishedAsync(finished);
                            break;
                    }
                }
                catch (OperationCanceledException) when (appLifetime.Stopping.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process playback command {CommandType}.", command.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await StopAllAsync();
        }
    }

    private async Task StartAsync(StartPlaybackCommand command)
    {
        if (_sessions.ContainsKey(command.GuildId))
        {
            logger.LogWarning("A playback session is already running for guild {GuildId}.", command.GuildId);
            return;
        }

        var voiceSession = voiceSessionManager.GetSession(command.GuildId);
        if (voiceSession is null)
        {
            logger.LogError("Cannot start playback for guild {GuildId}: voice session was not found.", command.GuildId);
            await AdvanceQueueAsync(command.GuildId);
            return;
        }

        var session = new PlaybackSession(playbackFactory, voiceSession, command.Track, appLifetime.Stopping);
        if (!_sessions.TryAdd(command.GuildId, session))
        {
            logger.LogWarning("A playback session is already running for guild {GuildId}.", command.GuildId);
            return;
        }

        try
        {
            logger.LogInformation(
                "Starting playback session for guild {GuildId}, Track: {TrackTitle}",
                command.GuildId,
                command.Track.Title);
            session.Start();
            _ = ObserveSessionAsync(command.GuildId, session);
        }
        catch (Exception ex)
        {
            RemoveIfCurrent(command.GuildId, session);
            logger.LogError(ex, "Failed to start playback for guild {GuildId}.", command.GuildId);
            await DisposeSessionAsync(command.GuildId, session);
            await AdvanceQueueAsync(command.GuildId);
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
            return;
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

        commandQueue.EnqueueFinished(guildId, session, shouldAdvanceQueue);
    }

    private async Task HandleFinishedAsync(PlaybackFinishedCommand command)
    {
        if (!RemoveIfCurrent(command.GuildId, command.Session))
            return;

        await DisposeSessionAsync(command.GuildId, command.Session);

        if (command.ShouldAdvanceQueue && !appLifetime.Stopping.IsCancellationRequested)
            await AdvanceQueueAsync(command.GuildId);
    }

    private async Task StopAsync(GuildId guildId)
    {
        if (!TryRemoveCurrent(guildId, out var session))
            return;

        await DisposeSessionAsync(guildId, session);
    }

    private async Task StopAllAsync()
    {
        foreach (var (guildId, session) in _sessions.ToArray())
        {
            if (RemoveIfCurrent(guildId, session))
                await DisposeSessionAsync(guildId, session);
        }
    }

    private bool RemoveIfCurrent(GuildId guildId, PlaybackSession session)
        => ((ICollection<KeyValuePair<GuildId, PlaybackSession>>)_sessions)
            .Remove(new KeyValuePair<GuildId, PlaybackSession>(guildId, session));

    private bool TryRemoveCurrent(GuildId guildId, out PlaybackSession session)
    {
        if (!_sessions.TryGetValue(guildId, out session!))
            return false;

        return RemoveIfCurrent(guildId, session);
    }

    private async Task DisposeSessionAsync(GuildId guildId, PlaybackSession session)
    {
        try
        {
            await session.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to dispose playback session for guild {GuildId}.", guildId);
        }
    }

    private async Task AdvanceQueueAsync(GuildId guildId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
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
}
