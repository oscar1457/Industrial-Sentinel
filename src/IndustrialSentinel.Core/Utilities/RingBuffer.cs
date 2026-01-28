namespace IndustrialSentinel.Core.Utilities;

public sealed class RingBuffer<T>
{
    private readonly T[] _items;
    private int _index;
    private bool _filled;
    private readonly object _sync = new();

    public RingBuffer(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _items = new T[capacity];
    }

    public int Capacity => _items.Length;

    public void Write(T item)
    {
        lock (_sync)
        {
            WriteUnsafe(item);
        }
    }

    public T[] Snapshot()
    {
        lock (_sync)
        {
            return SnapshotUnsafe();
        }
    }

    internal void WriteUnsafe(T item)
    {
        _items[_index] = item;
        _index++;
        if (_index >= _items.Length)
        {
            _index = 0;
            _filled = true;
        }
    }

    internal T[] SnapshotUnsafe()
    {
        var count = _filled ? _items.Length : _index;
        var result = new T[count];
        if (count == 0)
        {
            return result;
        }

        if (!_filled)
        {
            Array.Copy(_items, result, count);
            return result;
        }

        var tail = _items.Length - _index;
        Array.Copy(_items, _index, result, 0, tail);
        Array.Copy(_items, 0, result, tail, _index);
        return result;
    }
}
