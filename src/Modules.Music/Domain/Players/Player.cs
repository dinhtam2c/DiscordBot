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

    public void Join(ChannelId voiceChannelId)
    {
        if (voiceChannelId == VoiceChannelId)
            throw new InvalidOperationException("Player is already connected to this voice channel.");

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
        }

        Raise(new PlaybackStartedEvent(this));
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
        }

        Raise(new PlaybackStartedEvent(this));
    }

    public void Pause()
    {
        if (HasCurrentTrack)
            IsPaused = true;
    }

    public void Resume()
    {
        if (HasCurrentTrack)
            IsPaused = false;
    }

    public void Skip()
    {
        IsPaused = false;

        if (_queue.Count > 0)
        {
            CurrentQueueItem = _queue.Dequeue();
        }
        else
        {
            CurrentQueueItem = null;
        }

        if (HasCurrentTrack)
        {
            Raise(new TrackSkippedEvent(GuildId));
        }
    }

    public void FinishCurrentTrack()
    {
        IsPaused = false;
        if (LoopMode == LoopMode.Track && HasCurrentTrack)
            return;

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

    public void ClearQueue()
    {
        _queue.Clear();
    }

    public void ShuffleQueue()
    {
        _queue.Shuffle();
    }
}
