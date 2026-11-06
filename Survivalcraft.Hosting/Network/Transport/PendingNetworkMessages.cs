using System.Collections;

namespace Game.Network.Transport;

/// <summary>Owned native messages with bounded count and conservative payload accounting.</summary>
internal sealed class PendingNetworkMessages(
    int capacity = 2048,
    long capacityBytes = 16L * 1024 * 1024,
    Action<long>? bytesChanged = null
) : IReadOnlyList<OutboundPackage>
{
    private readonly List<(OutboundPackage Message, long Bytes)> _messages = [];

    public int Count => _messages.Count;

    public long PendingBytes { get; private set; }

    public OutboundPackage this[int index] => _messages[index].Message;

    internal long EstimateChange(OutboundPackage package)
    {
        var replacement = SnapshotPackageCoalescer.FindReplacement(this, package);
        return ProtocolMessageOwnership.EstimateQueueBytes(package.Package) -
               (replacement >= 0 ? _messages[replacement].Bytes : 0);
    }

    internal bool HasCoalescibleMessage(OutboundPackage package)
    {
        return SnapshotPackageCoalescer.FindReplacement(this, package) >= 0;
    }

    internal bool TryEnqueue(OutboundPackage package, bool prepend = false)
    {
        var bytes = ProtocolMessageOwnership.EstimateQueueBytes(package.Package);
        var replacement = SnapshotPackageCoalescer.FindReplacement(this, package);
        if (prepend && replacement >= 0) { return false; }

        var previous = replacement >= 0 ? _messages[replacement].Bytes : 0;
        if (bytes > capacityBytes - (PendingBytes - previous) || replacement < 0 && Count >= capacity)
        {
            return false;
        }

        if (replacement >= 0)
        {
            var retired = _messages[replacement].Message;
            _messages[replacement] = (package, bytes);
            Adjust(bytes - previous);
            retired.Fanout?.Advance(1, 0);
        }
        else
        {
            if (prepend) { _messages.Insert(0, (package, bytes)); }
            else { _messages.Add((package, bytes)); }

            Adjust(bytes);
        }

        return true;
    }

    internal void Replace(int index, OutboundPackage package)
    {
        var bytes = ProtocolMessageOwnership.EstimateQueueBytes(package.Package);
        var previous = _messages[index].Bytes;
        if (bytes > capacityBytes - (PendingBytes - previous))
        {
            throw new InvalidOperationException("A replacement exceeds its native queue capacity.");
        }

        _messages[index] = (package, bytes);
        Adjust(bytes - previous);
    }

    internal void RemoveAt(int index)
    {
        var bytes = _messages[index].Bytes;
        _messages.RemoveAt(index);
        Adjust(-bytes);
    }

    internal bool Remove(OutboundPackage package)
    {
        var index = _messages.FindIndex(entry => entry.Message == package);
        if (index < 0) { return false; }

        RemoveAt(index);
        return true;
    }

    internal void Clear()
    {
        _messages.Clear();
        Adjust(-PendingBytes);
    }

    private void Adjust(long change)
    {
        PendingBytes = checked(PendingBytes + change);
        bytesChanged?.Invoke(change);
    }

    public IEnumerator<OutboundPackage> GetEnumerator()
    {
        foreach (var entry in _messages) { yield return entry.Message; }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
