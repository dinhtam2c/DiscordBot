using Modules.Music.Players;
using Modules.Music.Tracks;

namespace Modules.Music.Playback;

internal sealed record PlaybackTarget(Guid? QueueItemId, Track? Track, bool IsPaused, Volume Volume)
{
    public static PlaybackTarget From(Player? player)
        => new(player?.CurrentQueueItem?.Id, player?.CurrentTrack, player?.IsPaused ?? false,
            player?.Volume ?? Volume.Create(50));
}
