using Domain.Core;
using Domain.ValueObjects;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Domain.Players;

internal sealed class Player : AggregateRoot
{
    public GuildId GuildId { get; init; }

    public ChannelId? VoiceChannelId { get; private set; }

    public QueueItem? CurrentQueueItem { get; private set; } = null;

    public Track? CurrentTrack => CurrentQueueItem?.Track;

    public Volume Volume { get; private set; } = Volume.Create(50);

    public LoopMode LoopMode { get; private set; }

    public bool IsConnected => VoiceChannelId is not null;

    public bool HasCurrentTrack => CurrentTrack is not null;

    public bool IsPlaying => IsConnected && HasCurrentTrack && !IsPaused;

    public bool IsPaused { get; private set; }

    private readonly PlaybackQueue _queue = new();

    public IReadOnlyList<QueueItem> Queue => _queue.Items;

    public bool HasQueue => _queue.Count > 0;

    public int QueueCount => _queue.Count;

    private Player(GuildId guildId)
    {
        GuildId = guildId;
    }

    public static Player Create(GuildId guildId)
    {
        return new Player(guildId);
    }

    internal Player CreateWorkingCopy()
    {
        var copy = new Player(GuildId)
        {
            VoiceChannelId = VoiceChannelId,
            CurrentQueueItem = CurrentQueueItem,
            Volume = Volume,
            LoopMode = LoopMode,
            IsPaused = IsPaused,
        };

        copy._queue.CopyFrom(_queue);
        return copy;
    }

    public void Join(ChannelId voiceChannelId)
    {
        if (voiceChannelId == VoiceChannelId)
            return;

        VoiceChannelId = voiceChannelId;
        Raise(new PlayerJoinedVoiceChannelEvent(GuildId, voiceChannelId));
    }

    public void Leave()
    {
        if (!IsConnected)
            return;

        Stop();
        VoiceChannelId = null;
        Raise(new PlayerLeftVoiceChannelEvent(GuildId));
    }

    public void Play(QueueItem item)
    {
        if (HasCurrentTrack)
        {
            _queue.Enqueue(item);
        }
        else
        {
            CurrentQueueItem = item;
            Raise(new PlaybackStartedEvent(GuildId, item.Track));
        }
    }

    public void PlayNext(QueueItem item)
    {
        if (HasCurrentTrack)
        {
            _queue.EnqueueFirst(item);
        }
        else
        {
            CurrentQueueItem = item;
            Raise(new PlaybackStartedEvent(GuildId, item.Track));
        }
    }

    public bool Pause()
    {
        if (!HasCurrentTrack || IsPaused)
            return false;

        IsPaused = true;
        Raise(new PlaybackPausedEvent(GuildId));
        return true;
    }

    public bool Resume()
    {
        if (!HasCurrentTrack || !IsPaused)
            return false;

        IsPaused = false;
        Raise(new PlaybackResumedEvent(GuildId));
        return true;
    }

    public void Skip()
    {
        IsPaused = false;

        var hasCurrentTrack = HasCurrentTrack;

        if (_queue.Count > 0)
        {
            CurrentQueueItem = _queue.Dequeue();
        }
        else
        {
            CurrentQueueItem = null;
        }

        if (hasCurrentTrack)
        {
            Raise(new TrackSkippedEvent(GuildId));
            if (CurrentTrack is { } nextTrack)
                Raise(new PlaybackStartedEvent(GuildId, nextTrack));
        }
    }

    public void FinishCurrentTrack()
    {
        IsPaused = false;
        if (LoopMode == LoopMode.Track && HasCurrentTrack)
        {
            Raise(new PlaybackStartedEvent(GuildId, CurrentTrack!));
            return;
        }

        if (_queue.Count > 0)
        {
            if (LoopMode == LoopMode.Queue && HasCurrentTrack)
            {
                _queue.Enqueue(CurrentQueueItem!);
            }

            CurrentQueueItem = _queue.Dequeue();
        }
        else
        {
            CurrentQueueItem = null;
        }

        if (CurrentTrack is { } nextTrack)
            Raise(new PlaybackStartedEvent(GuildId, nextTrack));
    }

    public void Stop()
    {
        IsPaused = false;

        if (!IsConnected || !HasCurrentTrack)
            return;

        CurrentQueueItem = null;
        _queue.Clear();
        Raise(new PlaybackStoppedEvent(GuildId));
    }

    public void SetVolume(Volume volume)
    {
        Volume = volume;
    }

    public void SetLoopMode(LoopMode loopMode)
    {
        LoopMode = loopMode;
    }

    public void RemoveFromQueue(int index)
    {
        _queue.RemoveAt(index);
    }

    public void MoveInQueue(int index, int newIndex)
    {
        _queue.Move(index, newIndex);
    }

    public void ClearQueue()
    {
        _queue.Clear();
    }

    public void ShuffleQueue()
    {
        _queue.Shuffle();
    }
}
