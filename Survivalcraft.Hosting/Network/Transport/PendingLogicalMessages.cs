using System.Collections;

namespace Game.Network.Transport;

/// <summary>Publication capacity remains reserved while a routing batch borrows its messages.</summary>
internal sealed class PendingLogicalMessages(
    int capacity = 8192,
    long capacityBytes = 64L * 1024 * 1024,
    int recipientCapacity = 2048,
    long recipientBytes = 16L * 1024 * 1024
) : IReadOnlyList<OutboundPackage>
{
    private readonly List<Lease> _pending = [];

    private readonly HashSet<Lease> _owned = [];

    private readonly Dictionary<WorldConnection, (int Count, long Bytes)> _recipients =
        new(ReferenceEqualityComparer.Instance);

    private long _sequence;

    public int Count => _pending.Count;

    internal int BufferedCount => _owned.Count;

    internal long PendingBytes { get; private set; }

    public OutboundPackage this[int index] => _pending[index].Package;

    internal static long Charge(IProtocolMessage package)
    {
        return package is TerrainSnapshotMessage snapshot
            ? checked(128L + snapshot.Snapshot.Cells.Length * 4L + snapshot.Snapshot.Shafts.Length * 8L)
            : ProtocolMessageOwnership.EstimateQueueBytes(package);
    }

    internal bool CanReserve(WorldConnection recipient, long bytes)
    {
        _recipients.TryGetValue(recipient, out var usage);
        return BufferedCount < capacity &&
               bytes <= capacityBytes - PendingBytes &&
               usage.Count < recipientCapacity &&
               bytes <= recipientBytes - usage.Bytes;
    }

    internal int CountSnapshots(WorldConnection recipient)
    {
        return _owned.Count(lease => lease.Package.Package is TerrainSnapshotMessage &&
                                     ReferenceEquals(lease.Package.Audience.SingleRecipient, recipient)
        );
    }

    internal bool CanEnqueue(OutboundPackage package)
    {
        var replacement = SnapshotPackageCoalescer.FindReplacement(this, package);
        var previous = replacement >= 0 ? _pending[replacement] : null;
        var bytes = Charge(package.Package);
        var recipient = package.Audience.SingleRecipient;
        (int Count, long Bytes) usage = default;
        if (recipient is not null) { _recipients.TryGetValue(recipient, out usage); }

        var oldBytes = previous?.Bytes ?? 0;
        var oldCount = previous is null ? 0 : 1;
        return BufferedCount - oldCount < capacity &&
               bytes <= capacityBytes - (PendingBytes - oldBytes) &&
               (recipient is null ||
                (usage.Count - oldCount < recipientCapacity && bytes <= recipientBytes - (usage.Bytes - oldBytes)));
    }

    internal bool TryEnqueue(OutboundPackage package, long? sequence = null)
    {
        if (!sequence.HasValue && TryAppendTerrainDeltas(package)) { return true; }

        var index = SnapshotPackageCoalescer.FindReplacement(this, package);
        if (sequence.HasValue && index >= 0)
        {
            package.Fanout?.Advance(1, 0);
            return true;
        }

        if (!CanEnqueue(package)) { return false; }

        var order = index >= 0 ? _pending[index].Sequence : sequence ?? ++_sequence;
        if (index >= 0) { Cancel(_pending[index]); }

        var lease = new Lease(package, Charge(package.Package), order);
        _owned.Add(lease);
        Adjust(lease, 1);
        Insert(lease);

        return true;
    }

    private bool TryAppendTerrainDeltas(OutboundPackage package)
    {
        if (package.Fanout is not null ||
            package.Package is not TerrainCellDeltaPackage newer ||
            _pending.Count == 0)
        {
            return false;
        }

        var tail = _pending[^1];
        if (tail.Package.Fanout is not null ||
            !ReferenceEquals(tail.Package.Runtime, package.Runtime) ||
            !tail.Package.Audience.HasSameRecipients(package.Audience) ||
            tail.Package.Package is not TerrainCellDeltaPackage previous ||
            previous.CellDeltas.Count + newer.CellDeltas.Count > TerrainCellDeltaPackage.MaximumDeltas)
        {
            return false;
        }

        var merged = new TerrainCellDeltaPackage
        {
            CellDeltas = previous.CellDeltas.Concat(newer.CellDeltas).ToArray()
        };
        var bytes = Charge(merged);
        var additional = bytes - tail.Bytes;
        if (additional > capacityBytes - PendingBytes) { return false; }

        if (package.Audience.SingleRecipient is { } recipient &&
            additional > recipientBytes - _recipients[recipient].Bytes)
        {
            return false;
        }

        Adjust(tail, -1);
        tail.Package = tail.Package with { Package = merged };
        tail.Bytes = bytes;
        Adjust(tail, 1);
        return true;
    }

    internal Lease[] Borrow(int maximumCount)
    {
        var result = _pending.Take(maximumCount).ToArray();
        _pending.RemoveRange(0, result.Length);
        foreach (var lease in result) { lease.Borrowed = true; }

        return result;
    }

    private void Insert(Lease lease)
    {
        var low = 0;
        var high = _pending.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_pending[middle].Sequence <= lease.Sequence) { low = middle + 1; }
            else { high = middle; }
        }

        _pending.Insert(low, lease);
    }

    internal void Return(Lease lease)
    {
        if (!lease.Active || !lease.Borrowed) { return; }

        lease.Borrowed = false;
        Insert(lease);
    }

    internal void Release(Lease lease)
    {
        if (!lease.Active) { return; }

        if (!_owned.Remove(lease))
        {
            throw new InvalidOperationException("The lease belongs to another logical queue.");
        }

        if (!lease.Borrowed) { _pending.Remove(lease); }

        lease.Active = false;
        Adjust(lease, -1);
    }

    internal void Cancel(Lease lease)
    {
        if (!lease.Active) { return; }

        if (lease.OwnsFanout) { lease.Package.Fanout?.Advance(1, 0); }

        Release(lease);
    }

    internal void Discard(WorldRuntimeContext runtime)
    {
        foreach (var lease in _owned.Where(lease => ReferenceEquals(lease.Package.Runtime, runtime)).ToArray())
        {
            Cancel(lease);
        }
    }

    internal void DiscardRecipient(WorldConnection client)
    {
        foreach (var lease in _owned.Where(lease => ReferenceEquals(lease.Package.Audience.SingleRecipient, client))
                     .ToArray())
        {
            Cancel(lease);
        }
    }

    internal void RemoveAt(int index) => Release(_pending[index]);

    internal void Replace(int index, OutboundPackage package)
    {
        var lease = _pending[index];
        var bytes = Charge(package.Package);
        if (bytes > lease.Bytes ||
            !ReferenceEquals(package.Runtime, lease.Package.Runtime) ||
            !package.Audience.HasSameRecipients(lease.Package.Audience))
        {
            throw new InvalidOperationException(
                "Trimming must preserve the publication owner and cannot increase its capacity."
            );
        }

        Adjust(lease, -1);
        lease.Package = package;
        lease.Bytes = bytes;
        Adjust(lease, 1);
    }

    internal void Clear()
    {
        foreach (var lease in _owned.ToArray())
        {
            // Pending callers retire their own fanouts; borrowed work is still owned here until dispatch begins.
            if (lease.Borrowed && lease.OwnsFanout) { lease.Package.Fanout?.Advance(1, 0); }

            Release(lease);
        }
    }

    private void Adjust(Lease lease, int direction)
    {
        PendingBytes = checked(PendingBytes + direction * lease.Bytes);
        if (lease.Package.Audience.SingleRecipient is not { } recipient) { return; }

        _recipients.TryGetValue(recipient, out var usage);
        usage = (usage.Count + direction, checked(usage.Bytes + direction * lease.Bytes));
        if (usage.Count == 0) { _recipients.Remove(recipient); }
        else { _recipients[recipient] = usage; }
    }

    public IEnumerator<OutboundPackage> GetEnumerator()
    {
        foreach (var lease in _pending) { yield return lease.Package; }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal sealed class Lease(OutboundPackage package, long bytes, long sequence)
    {
        internal long Sequence { get; } = sequence;

        internal OutboundPackage Package { get; set; } = package;

        internal long Bytes { get; set; } = bytes;

        internal bool Active { get; set; } = true;

        internal bool Borrowed { get; set; }

        internal bool OwnsFanout { get; set; } = true;
    }
}
