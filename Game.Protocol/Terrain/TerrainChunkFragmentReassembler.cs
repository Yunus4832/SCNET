using Engine.Core.Mathematics;

namespace Game.Protocol.Terrain;

public sealed class TerrainChunkFragmentReassembler
{
    public const int DefaultMaximumPendingTransfers = 64;

    public const long DefaultMaximumPendingBytes = 16L * 1024 * 1024;

    private const int _maximumCompletedTransfers = 4096;

    private readonly int _maximumPendingTransfers;

    private readonly long _maximumPendingBytes;

    private readonly Dictionary<ChunkAllocationId, Assembly> _assemblies = [];

    private readonly Dictionary<ChunkAllocationId, (long Version, long Sequence)> _completed = [];

    private readonly Queue<TransferKey> _completedOrder = [];

    private readonly Lock _lock = new();

    private long _reservedBytes;

    private long _completionSequence;

    public TerrainChunkFragmentReassembler(
        int maximumPendingTransfers = DefaultMaximumPendingTransfers,
        long maximumPendingBytes = DefaultMaximumPendingBytes
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPendingTransfers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPendingBytes);
        _maximumPendingTransfers = maximumPendingTransfers;
        _maximumPendingBytes = maximumPendingBytes;
    }

    public int PendingCount
    {
        get
        {
            lock (_lock) { return _assemblies.Count; }
        }
    }

    public long ReservedBytes
    {
        get
        {
            lock (_lock) { return _reservedBytes; }
        }
    }

    public bool Add(EncodedTerrainChunkFragment fragment, out EncodedTerrainChunk encoded)
    {
        lock (_lock)
        {
            Validate(fragment);
            encoded = null!;
            if (_completed.TryGetValue(fragment.Allocation, out var completedVersion) &&
                fragment.ContentVersion <= completedVersion.Version)
            {
                return false;
            }

            if (_assemblies.TryGetValue(fragment.Allocation, out var assembly))
            {
                if (fragment.ContentVersion < assembly.ContentVersion) { return false; }

                if (fragment.ContentVersion > assembly.ContentVersion)
                {
                    RemoveAssembly(fragment.Allocation);
                    assembly = null;
                }
                else if (assembly.TotalLength != fragment.TotalLength ||
                         assembly.Fragments.Length != fragment.FragmentCount)
                {
                    throw new InvalidDataException(
                        "A terrain transfer changed shape without changing its content version."
                    );
                }
            }

            if (assembly == null)
            {
                var reserved = 2L * fragment.TotalLength + fragment.FragmentCount * (IntPtr.Size + 32L) + 128;
                if (_assemblies.Count >= _maximumPendingTransfers || reserved > _maximumPendingBytes - _reservedBytes)
                {
                    return false;
                }

                assembly = new Assembly(
                    fragment.ContentVersion,
                    fragment.TotalLength,
                    fragment.FragmentCount,
                    reserved
                );
                _assemblies.Add(fragment.Allocation, assembly);
                _reservedBytes += reserved;
            }

            if (assembly.Fragments[fragment.FragmentIndex] == null)
            {
                assembly.Fragments[fragment.FragmentIndex] = fragment.Payload.ToArray();
                assembly.ReceivedCount++;
            }

            if (assembly.ReceivedCount != assembly.Fragments.Length) { return false; }

            var payload = new byte[assembly.TotalLength];
            var offset = 0;
            foreach (var part in assembly.Fragments)
            {
                part!.CopyTo(payload, offset);
                offset += part.Length;
            }

            RemoveAssembly(fragment.Allocation);
            var sequence = ++_completionSequence;
            _completed[fragment.Allocation] = (fragment.ContentVersion, sequence);
            _completedOrder.Enqueue(new TransferKey(fragment.Allocation, sequence));
            while (_completedOrder.Count > _maximumCompletedTransfers)
            {
                var old = _completedOrder.Dequeue();
                if (_completed.TryGetValue(old.Allocation, out var value) && value.Sequence == old.Sequence)
                {
                    _completed.Remove(old.Allocation);
                }
            }

            encoded = new EncodedTerrainChunk(fragment.Allocation.Coords, fragment.ContentVersion, payload);
            return true;
        }
    }

    public bool TryCreateMissingFragmentRequest(ChunkAllocationId allocation, out TerrainChunkFragmentRequest request)
    {
        lock (_lock)
        {
            if (!_assemblies.TryGetValue(allocation, out var assembly))
            {
                request = default;
                return false;
            }

            var missing = assembly.Fragments.Select((payload, index) => (payload, index))
                .Where(item => item.payload == null).Select(item => checked((ushort)item.index)).ToArray();
            request = new TerrainChunkFragmentRequest(
                allocation,
                assembly.ContentVersion,
                checked((ushort)assembly.Fragments.Length),
                missing
            );
            return missing.Length > 0;
        }
    }

    public void Discard(ChunkAllocationId allocation)
    {
        lock (_lock)
        {
            RemoveAssembly(allocation);
            _completed.Remove(allocation);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _assemblies.Clear();
            _completed.Clear();
            _completedOrder.Clear();
            _reservedBytes = 0;
        }
    }

    public int DiscardOtherGenerations(IEnumerable<ChunkAllocationId> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        lock (_lock)
        {
            var current = new Dictionary<Point2, ChunkAllocationId>();
            foreach (var allocation in allocations)
            {
                if (!current.TryGetValue(allocation.Coords, out var existing) ||
                    allocation.Generation > existing.Generation)
                {
                    current[allocation.Coords] = allocation;
                }
            }

            var obsolete = _assemblies.Keys.Where(allocation =>
                current.TryGetValue(allocation.Coords, out var active) && active != allocation
            ).ToArray();
            foreach (var allocation in obsolete) { RemoveAssembly(allocation); }

            foreach (var allocation in _completed.Keys.Where(allocation =>
                         current.TryGetValue(allocation.Coords, out var active) && active != allocation
                     ).ToArray())
            {
                _completed.Remove(allocation);
            }

            return obsolete.Length;
        }
    }

    private void RemoveAssembly(ChunkAllocationId allocation)
    {
        if (_assemblies.Remove(allocation, out var assembly)) { _reservedBytes -= assembly.ReservedBytes; }
    }

    private static void Validate(EncodedTerrainChunkFragment fragment)
    {
        var expectedCount = Math.Max(
            1,
            (fragment.TotalLength + ProtocolLimits.TerrainFragmentPayloadSize - 1) /
            ProtocolLimits.TerrainFragmentPayloadSize
        );
        var expectedLength = Math.Min(
            ProtocolLimits.TerrainFragmentPayloadSize,
            fragment.TotalLength - fragment.FragmentIndex * ProtocolLimits.TerrainFragmentPayloadSize
        );
        if (fragment.Allocation.Generation == 0 ||
            fragment.ContentVersion <= 0 ||
            fragment.TotalLength is < 0 or > ProtocolLimits.MaximumTerrainPayloadLength ||
            fragment.FragmentCount != expectedCount ||
            fragment.FragmentIndex >= fragment.FragmentCount ||
            fragment.Payload == null ||
            fragment.Payload.Length != expectedLength)
        {
            throw new InvalidDataException("Invalid terrain chunk fragment metadata.");
        }
    }

    private readonly record struct TransferKey(ChunkAllocationId Allocation, long Sequence);

    private sealed class Assembly(long contentVersion, int totalLength, ushort fragmentCount, long reservedBytes)
    {
        public long ContentVersion { get; } = contentVersion;

        public int TotalLength { get; } = totalLength;

        public long ReservedBytes { get; } = reservedBytes;

        public byte[][] Fragments { get; } = new byte[fragmentCount][];

        public int ReceivedCount;
    }
}
