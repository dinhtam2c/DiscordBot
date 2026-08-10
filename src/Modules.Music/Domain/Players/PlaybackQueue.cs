using System.Runtime.InteropServices;

namespace Modules.Music.Domain.Players;

public class PlaybackQueue
{
    private readonly List<QueueItem> _items = [];

    public IReadOnlyList<QueueItem> Items => _items.AsReadOnly();

    public int Count => _items.Count;

    public QueueItem this[int index] => _items[index];

    public void Enqueue(QueueItem item)
    {
        _items.Add(item);
    }

    public void EnqueueFirst(QueueItem item)
    {
        _items.Insert(0, item);
    }

    public QueueItem? Dequeue()
    {
        if (_items.Count == 0)
            return null;
        var item = _items[0];
        _items.RemoveAt(0);
        return item;
    }

    public QueueItem? Peek()
    {
        if (_items.Count == 0)
            return null;
        return _items[0];
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public void Shuffle()
    {
        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(_items));
    }
}
 