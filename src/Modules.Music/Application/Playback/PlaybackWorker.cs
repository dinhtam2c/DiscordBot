using Application.Abstractions;
using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modules.Music.Application.Playback;

internal sealed class PlaybackWorker(
    PlaybackCommandQueue commandQueue,
    IPlaybackFactory playbackFactory,
    IVoiceOutputProvider voiceOutputProvider,
    IAppLifetime appLifetime,
    IServiceScopeFactory scopeFactory,
    ILogger<PlaybackWorker> logger)
{
    private readonly Dictionary<GuildId, IPlaybackInstance> _sessions = new();

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
                            stop.Completion?.TrySetResult();
                            break;
                        case PausePlaybackCommand pause:
                            await PauseAsync(pause.GuildId);
                            break;
                        case ResumePlaybackCommand resume:
                            await ResumeAsync(resume.GuildId);
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
                    if (command is StopPlaybackCommand { Completion: { } completion })
                        completion.TrySetException(ex);

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

        IPlaybackInstance? playback = null;
        try
        {
            var voiceOutput = voiceOutputProvider.GetOutput(command.GuildId);

            logger.LogInformation(
                "Starting playback for guild {GuildId}, Track: {TrackTitle}",
                command.GuildId,
                command.Track.Title);
            playback = playbackFactory.Start(command.Track, voiceOutput, appLifetime.Stopping);
            _sessions.Add(command.GuildId, playback);
            _ = ObservePlaybackAsync(command.GuildId, playback);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start playback for guild {GuildId}.", command.GuildId);
            if (playback is not null)
            {
                RemoveIfCurrent(command.GuildId, playback);
                await DisposePlaybackAsync(command.GuildId, playback);
            }
            await AdvanceQueueAsync(command.GuildId);
        }
    }

    private async Task ObservePlaybackAsync(GuildId guildId, IPlaybackInstance playback)
    {
        var shouldAdvanceQueue = false;

        try
        {
            await playback.Completion;
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

        commandQueue.EnqueueFinished(guildId, playback, shouldAdvanceQueue);
    }

    private async Task HandleFinishedAsync(PlaybackFinishedCommand command)
    {
        if (!RemoveIfCurrent(command.GuildId, command.Playback))
            return;

        await DisposePlaybackAsync(command.GuildId, command.Playback);

        if (command.ShouldAdvanceQueue && !appLifetime.Stopping.IsCancellationRequested)
            await AdvanceQueueAsync(command.GuildId);
    }

    private async Task StopAsync(GuildId guildId)
    {
        if (!TryRemoveCurrent(guildId, out var playback))
            return;

        await DisposePlaybackAsync(guildId, playback);
    }

    private async Task PauseAsync(GuildId guildId)
    {
        if (_sessions.TryGetValue(guildId, out var playback))
            await playback.PauseAsync(appLifetime.Stopping);
    }

    private async Task ResumeAsync(GuildId guildId)
    {
        if (_sessions.TryGetValue(guildId, out var playback))
            await playback.ResumeAsync(appLifetime.Stopping);
    }

    private async Task StopAllAsync()
    {
        foreach (var (guildId, playback) in _sessions.ToArray())
        {
            if (RemoveIfCurrent(guildId, playback))
                await DisposePlaybackAsync(guildId, playback);
        }
    }

    private bool RemoveIfCurrent(GuildId guildId, IPlaybackInstance playback)
    {
        if (!_sessions.TryGetValue(guildId, out var current) || !ReferenceEquals(current, playback))
            return false;

        return _sessions.Remove(guildId);
    }

    private bool TryRemoveCurrent(GuildId guildId, out IPlaybackInstance playback)
    {
        if (!_sessions.TryGetValue(guildId, out playback!))
            return false;

        return RemoveIfCurrent(guildId, playback);
    }

    private async Task DisposePlaybackAsync(GuildId guildId, IPlaybackInstance playback)
    {
        try
        {
            await playback.DisposeAsync();
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
