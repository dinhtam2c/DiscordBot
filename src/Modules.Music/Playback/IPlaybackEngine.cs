using Domain.ValueObjects;

namespace Modules.Music.Playback;

internal interface IPlaybackEngine
{
    // State-changing calls for the same guild are serialized by its PlayerMailbox.
    Task ApplyAsync(GuildId guildId, PlaybackTarget target,
        Action<PlaybackStarted> onStarted, Action<PlaybackEnded> onEnded, CancellationToken ct);
    bool IsCurrent(GuildId guildId, Guid sessionId);
    Guid? GetSessionId(GuildId guildId, Guid queueItemId);
    // Read-only progress may also be sampled outside the mailbox, for the expected queue item only.
    TimeSpan? GetPosition(GuildId guildId, Guid queueItemId);
    Task CompleteAsync(GuildId guildId, Guid sessionId);

    Task SeekAsync(GuildId guildId, PlaybackTarget target, TimeSpan position,
        Tracks.AudioSources.ResolvedAudioSource source, Action<PlaybackEnded> onEnded, CancellationToken ct);
}

internal enum PlaybackEndReason { Completed, Failed }

internal sealed record PlaybackStarted(GuildId GuildId, Guid SessionId, Guid QueueItemId);

internal sealed record PlaybackEnded(GuildId GuildId, Guid SessionId, Guid QueueItemId,
    PlaybackEndReason Reason, Exception? Error = null);
