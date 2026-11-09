using Domain.ValueObjects;
using Modules.Music.Tracks;

namespace Modules.Music.Players;

// Detached, read-only data may leave a mailbox; its mutable Player never does.
internal sealed record PlayerSnapshot(
    ChannelId? VoiceChannelId,
    QueueItem? CurrentQueueItem,
    IReadOnlyList<QueueItem> Queue,
    bool IsPaused,
    Volume Volume,
    LoopMode LoopMode)
{
    public bool HasCurrentTrack => CurrentQueueItem is not null;
    public Track? CurrentTrack => CurrentQueueItem?.Track;
    public bool HasQueue => Queue.Count > 0;

    internal static PlayerSnapshot From(Player player)
        => new(player.VoiceChannelId, player.CurrentQueueItem, Array.AsReadOnly(player.Queue.ToArray()),
            player.IsPaused, player.Volume, player.LoopMode);
}
