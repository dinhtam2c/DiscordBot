using Modules.Music.Tracks;
using Domain.ValueObjects;

namespace Modules.Music.Players;

internal sealed class Player
{
    public GuildId GuildId { get; init; }

    public ChannelId? VoiceChannelId { get; private set; }

    public QueueItem? CurrentQueueItem { get; private set; } = null;

    public Track? CurrentTrack => CurrentQueueItem?.Track;

    public Volume Volume { get; private set; } = Volume.Create(50);

    public LoopMode LoopMode { get; private set; }

    public bool HasCurrentTrack => CurrentTrack is not null;

    public bool IsManuallyPaused { get; private set; }
    public bool IsPausedForEmptyChannel { get; private set; }
    public bool IsPaused => HasCurrentTrack && (IsManuallyPaused || IsPausedForEmptyChannel);

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
            IsManuallyPaused = IsManuallyPaused,
            IsPausedForEmptyChannel = IsPausedForEmptyChannel,
        };

        copy._queue.CopyFrom(_queue);
        return copy;
    }

    public void Join(ChannelId voiceChannelId)
    {
        if (voiceChannelId == VoiceChannelId)
            return;

        VoiceChannelId = voiceChannelId;
    }

    public void Leave()
    {
        if (VoiceChannelId is null)
            return;

        Stop();
        VoiceChannelId = null;
    }

    public void Enqueue(IReadOnlyList<QueueItem> items, bool playNext)
    {
        if (items.Count == 0)
            return;
        var start = 0;
        if (!HasCurrentTrack)
        {
            CurrentQueueItem = items[0];
            start = 1;
        }
        _queue.EnqueueRange(items.Skip(start), first: playNext);
    }

    public bool Pause()
    {
        if (!HasCurrentTrack || IsManuallyPaused)
            return false;

        IsManuallyPaused = true;
        return true;
    }

    public bool Resume()
    {
        if (!HasCurrentTrack || !IsManuallyPaused)
            return false;

        IsManuallyPaused = false;
        return true;
    }

    public bool SetHasListeners(bool hasListeners)
    {
        if (IsPausedForEmptyChannel == !hasListeners)
            return false;
        IsPausedForEmptyChannel = !hasListeners;
        return true;
    }

    public void Skip()
    {
        IsManuallyPaused = false;

        if (_queue.Count > 0)
        {
            CurrentQueueItem = _queue.Dequeue();
        }
        else
        {
            CurrentQueueItem = null;
        }
    }

    public void FinishCurrentTrack()
    {
        IsManuallyPaused = false;
        if (LoopMode == LoopMode.Track && HasCurrentTrack)
        {
            return;
        }

        if (LoopMode == LoopMode.Queue && HasCurrentTrack)
            _queue.Enqueue(CurrentQueueItem!);

        CurrentQueueItem = _queue.Dequeue();
    }

    public void Stop()
    {
        IsManuallyPaused = false;

        CurrentQueueItem = null;
        _queue.Clear();
    }

    public bool SetVolume(Volume volume)
    {
        if (Volume == volume)
            return false;

        Volume = volume;
        return true;
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
