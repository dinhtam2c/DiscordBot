using System.Collections.Concurrent;
using Application.Discord.Voice;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Modules.Music.Playback.Native;

internal sealed class PlaybackEngine(
    IPlaybackFactory factory,
    IVoiceOutputProvider voiceOutputs,
    ILogger<PlaybackEngine> logger) : IPlaybackEngine
{
    private readonly ConcurrentDictionary<GuildId, Session> _sessions = new();

    public async Task ApplyAsync(GuildId guildId, PlaybackTarget target,
        Action<PlaybackStarted> onStarted, Action<PlaybackEnded> onEnded, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _sessions.TryGetValue(guildId, out var current);

        if (current is not null && current.QueueItemId != target.QueueItemId)
        {
            await CompleteAsync(guildId, current.Id);
            current = null;
        }

        if (target.Track is null || target.QueueItemId is null)
            return;

        if (current is null)
        {
            current = new Session(Guid.NewGuid(), target.QueueItemId.Value);
            _sessions[guildId] = current;
            try
            {
                logger.LogInformation("Starting playback for guild {GuildId} with track '{TrackTitle}'.", guildId, target.Track.Title);
                current.Playback = factory.Start(target.Track, target.Volume, voiceOutputs.GetOutput(guildId), ct,
                    isPaused: target.IsPaused);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _sessions.TryRemove(guildId, out _);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start playback for guild {GuildId}.", guildId);
                // Keep this identity until the queued completion is handled. Never recurse into Player.
                ReportEnded(guildId, current, PlaybackEndReason.Failed, onEnded, ex);
                return;
            }

            onStarted(new PlaybackStarted(guildId, current.Id, current.QueueItemId));
            _ = ObserveAsync(guildId, current, onEnded, ct);
        }

        if (current.Playback is not { } playback)
            return;

        try
        {
            await playback.SetVolumeAsync(target.Volume, ct);
            if (target.IsPaused)
                await playback.PauseAsync(ct);
            else
                await playback.ResumeAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update playback for guild {GuildId}.", guildId);
            ReportEnded(guildId, current, PlaybackEndReason.Failed, onEnded, ex);
        }
    }

    public bool IsCurrent(GuildId guildId, Guid sessionId)
        => _sessions.TryGetValue(guildId, out var current) && current.Id == sessionId;

    public Guid? GetSessionId(GuildId guildId, Guid queueItemId)
        => _sessions.TryGetValue(guildId, out var current) && current.QueueItemId == queueItemId
            && current.Playback is not null ? current.Id : null;

    public async Task SeekAsync(GuildId guildId, PlaybackTarget target, TimeSpan position,
        Tracks.AudioSources.ResolvedAudioSource source, Action<PlaybackEnded> onEnded, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (target.Track is null || target.QueueItemId is null)
            throw new InvalidOperationException("There is no current track to seek.");
        if (_sessions.TryGetValue(guildId, out var previous))
            await CompleteAsync(guildId, previous.Id);

        var output = voiceOutputs.GetOutput(guildId);
        var current = new Session(Guid.NewGuid(), target.QueueItemId.Value);
        _sessions[guildId] = current;
        try
        {
            await output.ClearAsync(ct);
            logger.LogInformation("Seeking to {PositionSeconds} seconds in guild {GuildId} with track '{TrackTitle}'.",
                position.TotalSeconds, guildId, target.Track.Title);
            current.Playback = factory.Start(target.Track, target.Volume, output, ct,
                target.IsPaused, position, source);
            _ = ObserveAsync(guildId, current, onEnded, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _sessions.TryRemove(guildId, out _);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seek audio for guild {GuildId}.", guildId);
            ReportEnded(guildId, current, PlaybackEndReason.Failed, onEnded, ex);
            throw;
        }
    }

    public TimeSpan? GetPosition(GuildId guildId, Guid queueItemId)
        => _sessions.TryGetValue(guildId, out var current) && current.QueueItemId == queueItemId
            ? current.Playback?.Position
            : null;

    public async Task CompleteAsync(GuildId guildId, Guid sessionId)
    {
        if (!IsCurrent(guildId, sessionId) || !_sessions.TryRemove(guildId, out var session))
            return;

        if (session.Playback is not { } playback)
            return;

        try { await playback.DisposeAsync(); }
        catch (Exception ex) { logger.LogError(ex, "Failed to dispose playback for guild {GuildId}.", guildId); }
    }

    private async Task ObserveAsync(GuildId guildId, Session session, Action<PlaybackEnded> onEnded, CancellationToken ct)
    {
        var reason = PlaybackEndReason.Completed;
        Exception? error = null;
        try { await session.Playback!.Completion; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Stopped playback for guild {GuildId}.", guildId);
            return;
        }
        catch (Exception ex)
        {
            reason = PlaybackEndReason.Failed;
            error = ex;
            logger.LogError(ex, "Failed to play audio for guild {GuildId}.", guildId);
        }

        if (!ct.IsCancellationRequested)
            ReportEnded(guildId, session, reason, onEnded, error);
    }

    private static void ReportEnded(GuildId guildId, Session session, PlaybackEndReason reason,
        Action<PlaybackEnded> onEnded, Exception? error = null)
    {
        if (session.TryReportCompletion())
            onEnded(new PlaybackEnded(guildId, session.Id, session.QueueItemId, reason, error));
    }

    private sealed class Session(Guid id, Guid queueItemId)
    {
        private int _completionReported;

        public Guid Id { get; } = id;
        public Guid QueueItemId { get; } = queueItemId;
        public IPlaybackInstance? Playback { get; set; }

        public bool TryReportCompletion() => Interlocked.Exchange(ref _completionReported, 1) == 0;
    }
}
