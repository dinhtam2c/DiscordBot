using Domain.ValueObjects;
using Modules.Music.Tracks;

namespace Modules.Music.Players.Coordination;

internal abstract record PlayerRequest;
internal sealed record JoinVoice(ChannelId ChannelId) : PlayerRequest;
internal sealed record LeaveVoice : PlayerRequest;
internal sealed record EnqueueTracks(IReadOnlyList<Track> Tracks, UserId RequesterId, bool PlayNext) : PlayerRequest;
internal sealed record PausePlayback : PlayerRequest;
internal sealed record ResumePlayback : PlayerRequest;
internal sealed record SkipTrack : PlayerRequest;
internal sealed record StopPlayback : PlayerRequest;
internal sealed record SetVolume(Volume Volume) : PlayerRequest;
internal sealed record SetLoopMode(LoopMode Mode) : PlayerRequest;
internal sealed record ClearQueue : PlayerRequest;
internal sealed record ShuffleQueue : PlayerRequest;
internal sealed record RemoveTrack(long Index) : PlayerRequest;
internal sealed record MoveTrack(long Index, long NewIndex) : PlayerRequest;

internal sealed record PlayerResult(PlayerSnapshot? Before, PlayerSnapshot? After);
