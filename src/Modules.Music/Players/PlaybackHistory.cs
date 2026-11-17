using Modules.Music.Tracks;
using Domain.ValueObjects;

namespace Modules.Music.Players;

internal enum PlaybackHistoryStatus { Playing, Completed, Skipped, Failed, Stopped }

internal sealed record PlaybackHistoryEntry(Guid SessionId, QueueItem Item,
    DateTimeOffset StartedAt, DateTimeOffset? EndedAt, PlaybackHistoryStatus Status)
{
    public UserId? RequesterId => Item.RequesterId;
    public UserId? SkippedBy { get; init; }
}

// One bounded history per guild. Read and written exclusively inside its mailbox.
internal sealed class PlaybackHistory(int capacity)
{
    private readonly List<PlaybackHistoryEntry> _entries = [];

    public IReadOnlyList<PlaybackHistoryEntry> Snapshot() => Array.AsReadOnly(_entries.AsEnumerable().Reverse().ToArray());
    public Track[] RecentTracks(int count) => _entries.TakeLast(count).Select(entry => entry.Item.Track).ToArray();

    public void Start(Guid sessionId, QueueItem item, DateTimeOffset now)
    {
        if (_entries.Any(entry => entry.SessionId == sessionId)) return;
        var active = _entries.FindIndex(entry => entry.Item.Id == item.Id && entry.Status == PlaybackHistoryStatus.Playing);
        if (active >= 0)
        {
            _entries[active] = _entries[active] with { SessionId = sessionId };
            return;
        }
        _entries.Add(new PlaybackHistoryEntry(sessionId, item, now, null, PlaybackHistoryStatus.Playing));
        if (_entries.Count > capacity) _entries.RemoveAt(0);
    }

    public void Finish(Guid sessionId, QueueItem item, PlaybackHistoryStatus status, DateTimeOffset now, UserId? skippedBy = null)
    {
        Start(sessionId, item, now); // Also capture attempts that failed before a started callback.
        var index = _entries.FindIndex(entry => entry.SessionId == sessionId);
        if (_entries[index].Status != PlaybackHistoryStatus.Playing) return;
        _entries[index] = _entries[index] with { Status = status, EndedAt = now, SkippedBy = status == PlaybackHistoryStatus.Skipped ? skippedBy : null };
    }

    public void Rebind(Guid oldSessionId, Guid newSessionId)
    {
        var index = _entries.FindIndex(entry => entry.SessionId == oldSessionId);
        if (index >= 0) _entries[index] = _entries[index] with { SessionId = newSessionId };
    }
}
