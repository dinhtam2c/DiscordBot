using Domain.ValueObjects;
using Modules.Music.Tracks;

namespace Modules.Music.Players.Coordination;

internal abstract record PlayerRequest;
internal sealed record JoinVoice(ChannelId ChannelId) : PlayerRequest;
internal sealed record LeaveVoice : PlayerRequest;
internal sealed record EnqueueTracks(IReadOnlyList<Track> Tracks, UserId RequesterId, bool PlayNext,
    long? PreparationRevision = null) : PlayerRequest;
internal sealed record PausePlayback : PlayerRequest;
internal sealed record ResumePlayback : PlayerRequest;
internal sealed record RefreshVoicePresence : PlayerRequest;
internal sealed record SeekPreparation(QueueItem Item, long Revision, Guid SessionId);
internal sealed record SeekPlayback(TimeSpan Position, Tracks.AudioSources.ResolvedAudioSource Source,
    SeekPreparation Preparation) : PlayerRequest
{
    public static void ValidatePosition(TimeSpan position, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            throw new Application.Abstractions.UserFriendlyException("This song doesn't support seeking.");
        if (position < TimeSpan.Zero || position >= duration)
            throw new Application.Abstractions.UserFriendlyException("Choose a position from 0:00 to before the end of this song.");
    }
}
internal sealed record SkipTrack : PlayerRequest;
internal sealed record StopPlayback : PlayerRequest;
internal sealed record SetVolume(Volume Volume) : PlayerRequest;
internal sealed record SetLoopMode(LoopMode Mode) : PlayerRequest;
internal sealed record ClearQueue : PlayerRequest;
internal sealed record ShuffleQueue : PlayerRequest;
internal sealed record RemoveTrack(long Index) : PlayerRequest;
internal sealed record MoveTrack(long Index, long NewIndex) : PlayerRequest;

internal sealed record PlayerResult(QueueItem? PreviousItem, bool WasPaused, int QueueCount, bool StartsPlayback)
{
    public bool HadCurrentTrack => PreviousItem is not null;
    public bool IsPausedForEmptyChannel { get; init; }
    public bool WasManuallyPaused { get; init; }
}
