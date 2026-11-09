using Domain.ValueObjects;

namespace Modules.Music.Playback;

internal interface IPlaybackEngine
{
    // Calls for the same guild are serialized by its PlayerMailbox.
    Task ApplyAsync(GuildId guildId, PlaybackTarget target,
        Action<PlaybackStarted> onStarted, Action<PlaybackEnded> onEnded, CancellationToken ct);
    bool IsCurrent(GuildId guildId, Guid sessionId);
    Task CompleteAsync(GuildId guildId, Guid sessionId);
}

internal enum PlaybackEndReason { Completed, Failed }

internal sealed record PlaybackStarted(GuildId GuildId, Guid SessionId, Guid QueueItemId);

internal sealed record PlaybackEnded(GuildId GuildId, Guid SessionId, Guid QueueItemId,
    PlaybackEndReason Reason, Exception? Error = null);
