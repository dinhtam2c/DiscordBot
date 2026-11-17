using Modules.Music.Players;
using Modules.Music.Tracks;

namespace Modules.Music.Features.Autoplay;

// All mutable state belongs to the guild mailbox, including acceptance of background results.
internal sealed class AutoplaySession
{
    public Task Preparation { get; set; } = Task.CompletedTask;
    public Track? Seed { get; private set; }
    public Guid? LastItemId { get; private set; }
    public bool Active { get; set; }
    public int Failures { get; set; }
    public int LoadedLimit { get; set; }
    public int PendingEnqueues { get; set; }
    public Queue<Track> Candidates { get; } = new();
    public Track? Ready { get; set; }
    public CancellationTokenSource? Work { get; private set; }
    public long Revision { get; private set; }

    public void Cancel()
    {
        Revision++;
        Work?.Cancel();
        Work = null; // The background operation owns disposal.
    }

    public void Observe(QueueItem? item)
    {
        if (item is null || item.Id == LastItemId) return;
        LastItemId = item.Id;
        if (!item.IsAutoplay)
        {
            Cancel();
            Seed = item.Track;
            Candidates.Clear();
            Ready = null;
            LoadedLimit = 0;
            Failures = 0;
            Active = true;
        }
    }

    public void ResetSeed()
    {
        Seed = null;
        LastItemId = null;
        Candidates.Clear();
        Ready = null;
        LoadedLimit = 0;
        Failures = 0;
    }

    public (long Revision, CancellationTokenSource Work) Begin(CancellationToken stopping)
    {
        Cancel();
        Work = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        return (Revision, Work);
    }

    public void Complete() => Work = null;
}
