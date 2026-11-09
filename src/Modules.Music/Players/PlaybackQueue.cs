using System.Runtime.InteropServices;

namespace Modules.Music.Players;

internal sealed class PlaybackQueue
{
    private readonly List<QueueItem> _items = [];

    public IReadOnlyList<QueueItem> Items => _items.AsReadOnly();

    public int Count => _items.Count;

    public void Enqueue(QueueItem item)
    {
        _items.Add(item);
    }

    public void EnqueueRange(IEnumerable<QueueItem> items, bool first)
    {
        if (first)
            _items.InsertRange(0, items);
        else
            _items.AddRange(items);
    }

    public QueueItem? Dequeue()
    {
        if (_items.Count == 0)
            return null;
        var item = _items[0];
        _items.RemoveAt(0);
        return item;
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
    }

    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _items.Count)
            throw new ArgumentOutOfRangeException(nameof(oldIndex), "The source queue index is out of range.");
        if (newIndex < 0 || newIndex >= _items.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex), "The destination queue index is out of range.");
        var item = _items[oldIndex];
        _items.RemoveAt(oldIndex);
        _items.Insert(newIndex, item);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public void CopyFrom(PlaybackQueue other)
    {
        _items.Clear();
        _items.AddRange(other._items);
    }

    public void Shuffle()
    {
        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(_items));
    }
}
