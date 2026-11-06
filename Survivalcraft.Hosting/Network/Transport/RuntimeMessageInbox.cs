namespace Game.Network.Transport;

/// <summary>Validates and accounts for game protocol messages before owned world delivery.</summary>
public sealed class RuntimeMessageInbox
{
    public const long DefaultCapacityBytes = OwnedMessageInbox<ReceivedPackage>.DefaultCapacityBytes;

    public const long DefaultDrainBytes = OwnedMessageInbox<ReceivedPackage>.DefaultDrainBytes;

    private readonly WorldRuntimeContext _runtime;

    private readonly object _localSender = new();

    private readonly OwnedMessageInbox<ReceivedPackage> _messages;

    public RuntimeMessageInbox(
        WorldRuntimeContext runtime,
        int capacity = 1024,
        long capacityBytes = DefaultCapacityBytes,
        TimeProvider? timeProvider = null
    )
    {
        _runtime = runtime;
        _messages = new OwnedMessageInbox<ReceivedPackage>(
            () => runtime.IsClosed,
            static message => (message.Package as IDisposable)?.Dispose(),
            capacity,
            capacityBytes,
            timeProvider
        );
    }

    public int Count => _messages.Count;

    public long PendingBytes => _messages.PendingBytes;

    public bool TryPostOwned(ReceivedPackage message, CancellationToken connectionClosed = default)
    {
        if (!ReferenceEquals(message.Context.Runtime, _runtime))
        {
            throw new InvalidOperationException("A message must target the runtime that owns this inbox.");
        }

        long charge;
        if (message.Package is TerrainSnapshotMessage snapshot)
        {
            charge = checked(128L + snapshot.Snapshot.Cells.Length * 4L + snapshot.Snapshot.Shafts.Length * 8L);
        }
        else
        {
            ProtocolMessageValidation.Validate(message.Package);
            charge = ProtocolMessageOwnership.EstimateQueueBytes(message.Package);
        }

        return _messages.TryPostOwned(
            message,
            (object?)message.Context.Sender ?? _localSender,
            charge,
            connectionClosed
        );
    }

    public int Drain(
        int maximumMessages,
        Action<ReceivedPackage> deliver,
        long maximumBytes = DefaultDrainBytes,
        TimeSpan? maximumTime = null
    )
    {
        return _messages.Drain(maximumMessages, deliver, maximumBytes, maximumTime);
    }

    internal void Clear() => _messages.Clear();
}
