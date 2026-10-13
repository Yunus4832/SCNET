namespace Engine.Network;

/// <summary>Preserves notification order while reserving independent capacity for lifecycle events.</summary>
internal sealed class NetworkNotificationQueue(int dataCapacity, int byteCapacity, int controlCapacity)
{
    private readonly Lock _gate = new();
    private readonly Queue<Entry> _queue = new();
    private int _dataCount;
    private int _controlCount;
    private int _bytes;

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    public bool TryEnqueue(Action action, int bytes = 0, bool control = false)
    {
        lock (_gate)
        {
            if (control ? _controlCount >= controlCapacity : _dataCount >= dataCapacity || bytes > byteCapacity - _bytes)
            {
                return false;
            }
            _queue.Enqueue(new Entry(action, bytes, control));
            if (control)
            {
                _controlCount++;
            }
            else
            {
                _dataCount++;
                _bytes += bytes;
            }
            return true;
        }
    }

    public bool TryDequeue(out Action? action)
    {
        lock (_gate)
        {
            if (!_queue.TryDequeue(out var entry))
            {
                action = null;
                return false;
            }
            if (entry.Control)
            {
                _controlCount--;
            }
            else
            {
                _dataCount--;
                _bytes -= entry.Bytes;
            }
            action = entry.Action;
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _queue.Clear();
            _dataCount = 0;
            _controlCount = 0;
            _bytes = 0;
        }
    }

    private sealed record Entry(Action Action, int Bytes, bool Control);
}
