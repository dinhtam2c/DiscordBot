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

    public async Task ApplyAsync(GuildId guildId, PlaybackTarget target, Action<PlaybackEnded> onEnded, CancellationToken ct)
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
                logger.LogInformation("Starting playback for guild {GuildId}, Track: {TrackTitle}", guildId, target.Track.Title);
                current.Playback = factory.Start(target.Track, target.Volume, voiceOutputs.GetOutput(guildId), ct);
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
                onEnded(new PlaybackEnded(guildId, current.Id));
                return;
            }

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
            onEnded(new PlaybackEnded(guildId, current.Id));
        }
    }

    public bool IsCurrent(GuildId guildId, Guid sessionId)
        => _sessions.TryGetValue(guildId, out var current) && current.Id == sessionId;

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
        try { await session.Playback!.Completion; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Playback session for guild {GuildId} was stopped.", guildId);
        }
        catch (Exception ex) { logger.LogError(ex, "Playback failed for guild {GuildId}.", guildId); }

        if (!ct.IsCancellationRequested)
            onEnded(new PlaybackEnded(guildId, session.Id));
    }

    private sealed class Session(Guid id, Guid queueItemId)
    {
        public Guid Id { get; } = id;
        public Guid QueueItemId { get; } = queueItemId;
        public IPlaybackInstance? Playback { get; set; }
    }
}
