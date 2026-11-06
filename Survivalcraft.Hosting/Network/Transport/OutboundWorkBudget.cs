namespace Game.Network.Transport;

/// <summary>Cooperative budget for one logical-routing or native-dispatch phase.</summary>
public sealed class OutboundWorkBudget(TimeProvider time)
{
    public const int MaximumMessages = 1024;

    private const long _maximumBytes = 4L * 1024 * 1024;

    private readonly long _started = time.GetTimestamp();

    private int _messages;

    private long _bytes;

    public bool HasTimeElapsed => time.GetElapsedTime(_started) >= TimeSpan.FromMilliseconds(4);

    public bool TryConsume(IProtocolMessage message)
    {
        var bytes = message is TerrainSnapshotMessage snapshot
            ? checked(128L + snapshot.Snapshot.Cells.Length * 4L + snapshot.Snapshot.Shafts.Length * 8L)
            : ProtocolMessageOwnership.EstimateQueueBytes(message);
        if (_messages > 0 &&
            (_messages >= MaximumMessages ||
             _bytes > _maximumBytes - bytes ||
             HasTimeElapsed))
        {
            return false;
        }

        _messages++;
        _bytes = checked(_bytes + bytes);
        return true;
    }
}
