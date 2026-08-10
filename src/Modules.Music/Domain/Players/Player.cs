using Domain.Core;
using Domain.ValueObjects;
using Modules.Music.Domain.Players.Events;

namespace Modules.Music.Domain.Players;

internal sealed class Player : AggregateRoot
{
    public GuildId GuildId { get; init; }

    public ChannelId? VoiceChannelId { get; private set; }

    public QueueItem? CurrentTrack { get; private set; } = null;

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
            CurrentTrack = item;
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
            CurrentTrack = item;
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

    public QueueItem? Skip()
    {
        IsPaused = false;

        if (HasCurrentTrack)
        {
            Raise(new TrackSkippedEvent(GuildId));
        }

        if (_queue.Count > 0)
        {
            CurrentTrack = _queue.Dequeue();
            return CurrentTrack;
        }
        else
        {
            CurrentTrack = null;
            return null;
        }
    }

    public QueueItem? FinishCurrentTrack()
    {
        IsPaused = false;
        if (LoopMode == LoopMode.Track && HasCurrentTrack)
        {
            return CurrentTrack;
        }

        if (_queue.Count > 0)
        {
            if (LoopMode == LoopMode.Queue && HasCurrentTrack)
            {
                _queue.Enqueue(CurrentTrack!);
            }

            CurrentTrack = _queue.Dequeue();
            return CurrentTrack;
        }
        else
        {
            CurrentTrack = null;
            return null;
        }
    }

    public void Stop()
    {
        IsPaused = false;

        if (!IsConnected || !HasCurrentTrack)
            return;

        CurrentTrack = null;
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
