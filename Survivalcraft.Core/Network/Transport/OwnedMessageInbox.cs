namespace Game.Network.Transport;

/// <summary>Owned, bounded, non-reentrant delivery with FIFO per sender and round-robin between senders.</summary>
public sealed class OwnedMessageInbox<TMessage>
{
    public const long DefaultCapacityBytes = 128L * 1024 * 1024;

    public const long DefaultDrainBytes = 2L * 1024 * 1024;

    private readonly Func<bool> _isClosed;

    private readonly Action<TMessage> _release;

    private readonly int _capacity;

    private readonly long _capacityBytes;

    private readonly TimeProvider _time;

    private readonly Lock _gate = new();

    private readonly Dictionary<object, SenderQueue> _senders = new(ReferenceEqualityComparer.Instance);

    private readonly LinkedList<SenderQueue> _ready = [];

    private int _count;

    private long _bytes;

    private long _sequence;

    private bool _draining;

    public OwnedMessageInbox(
        Func<bool> isClosed,
        Action<TMessage> release,
        int capacity = 1024,
        long capacityBytes = DefaultCapacityBytes,
        TimeProvider? timeProvider = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityBytes);
        ArgumentNullException.ThrowIfNull(isClosed);
        ArgumentNullException.ThrowIfNull(release);
        _isClosed = isClosed;
        _release = release;
        _capacity = capacity;
        _capacityBytes = capacityBytes;
        _time = timeProvider ?? TimeProvider.System;
    }

    public int Count
    {
        get
        {
            lock (_gate) { return _count; }
        }
    }

    public long PendingBytes
    {
        get
        {
            lock (_gate) { return _bytes; }
        }
    }

    public bool TryPostOwned(
        TMessage message,
        object sender,
        long charge,
        CancellationToken connectionClosed = default
    )
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(charge);

        var purged = false;
        while (true)
        {
            List<Entry>? cancelled = null;
            lock (_gate)
            {
                if (_isClosed() || connectionClosed.IsCancellationRequested || charge > _capacityBytes)
                {
                    return false;
                }

                if (_count >= _capacity || _bytes > _capacityBytes - charge)
                {
                    if (purged) { return false; }

                    cancelled = PurgeCancelled();
                    if (cancelled.Count == 0) { return false; }
                }
                else
                {
                    if (!_senders.TryGetValue(sender, out var queue))
                    {
                        queue = new SenderQueue(sender);
                        queue.Node = _ready.AddLast(queue);
                        _senders.Add(sender, queue);
                    }

                    queue.Pending.Enqueue(new Entry(message, connectionClosed, charge, ++_sequence));
                    _count++;
                    _bytes += charge;
                    return true;
                }
            }

            // A release fault must occur before this call accepts ownership of the new message.
            // Callbacks run outside the gate and may consume capacity or close the world;
            // retry under the gate only after every removed entry has been released successfully.
            ReleaseBatch(cancelled);
            // One post cleans at most one captured batch, even if a release callback
            // refills the inbox with more cancelled work.
            purged = true;
        }
    }

    private List<Entry> PurgeCancelled()
    {
        var discarded = new List<Entry>();
        foreach (var queue in _ready.ToArray())
        {
            var count = queue.Pending.Count;
            for (var index = 0; index < count; index++)
            {
                var entry = queue.Pending.Dequeue();
                if (entry.ConnectionClosed.IsCancellationRequested)
                {
                    _count--;
                    _bytes -= entry.Bytes;
                    discarded.Add(entry);
                }
                else { queue.Pending.Enqueue(entry); }
            }

            if (queue.Pending.Count == 0) { Remove(queue); }
        }

        return discarded;
    }

    private void Remove(SenderQueue queue)
    {
        _ready.Remove(queue.Node!);
        _senders.Remove(queue.Sender);
    }

    public int Drain(
        int maximumMessages,
        Action<TMessage> deliver,
        long maximumBytes = DefaultDrainBytes,
        TimeSpan? maximumTime = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentNullException.ThrowIfNull(deliver);
        var budget = maximumTime ?? TimeSpan.FromMilliseconds(4);
        if (budget <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(maximumTime)); }

        long cutoff;
        lock (_gate)
        {
            if (_draining || _isClosed()) { return 0; }

            _draining = true;
            cutoff = _sequence;
        }

        var started = _time.GetTimestamp();
        var inspected = 0;
        var delivered = 0;
        var consumedBytes = 0L;
        try
        {
            while (inspected < maximumMessages)
            {
                if (inspected > 0 && _time.GetElapsedTime(started) >= budget) { break; }

                Entry entry;
                lock (_gate)
                {
                    if (_isClosed() || _ready.First == null) { break; }

                    var scanned = 0;
                    while (_ready.First.Value.Pending.Peek().Sequence > cutoff && scanned < _ready.Count)
                    {
                        var deferred = _ready.First;
                        _ready.RemoveFirst();
                        _ready.AddLast(deferred);
                        scanned++;
                    }

                    if (scanned == _ready.Count) { break; }

                    var queue = _ready.First.Value;
                    entry = queue.Pending.Peek();
                    if (inspected > 0 && entry.Bytes > maximumBytes - consumedBytes) { break; }

                    queue.Pending.Dequeue();
                    _count--;
                    _bytes -= entry.Bytes;
                    if (queue.Pending.Count == 0) { Remove(queue); }
                    else
                    {
                        _ready.Remove(queue.Node!);
                        _ready.AddLast(queue.Node!);
                    }
                }

                inspected++;
                consumedBytes = checked(consumedBytes + entry.Bytes);
                try
                {
                    if (entry.ConnectionClosed.IsCancellationRequested) { continue; }

                    deliver(entry.Message);
                    delivered++;
                }
                finally { Release(entry); }
            }
        }
        finally
        {
            lock (_gate) { _draining = false; }
        }

        return delivered;
    }

    public void Clear()
    {
        Entry[] entries;
        lock (_gate)
        {
            entries = _ready.SelectMany(queue => queue.Pending).ToArray();
            _ready.Clear();
            _senders.Clear();
            _count = 0;
            _bytes = 0;
        }

        ReleaseBatch(entries);
    }

    private void ReleaseBatch(IEnumerable<Entry> entries)
    {
        List<Exception>? errors = null;
        foreach (var entry in entries)
        {
            try { Release(entry); }
            catch (Exception exception) { (errors ??= []).Add(exception); }
        }

        if (errors != null) { throw new AggregateException("Could not release all owned inbox messages.", errors); }
    }

    private void Release(Entry entry) => _release(entry.Message);

    private sealed class SenderQueue(object sender)
    {
        internal object Sender { get; } = sender;

        internal Queue<Entry> Pending { get; } = new();

        internal LinkedListNode<SenderQueue>? Node { get; set; }
    }

    private readonly record struct Entry(
        TMessage Message,
        CancellationToken ConnectionClosed,
        long Bytes,
        long Sequence
    );
}
