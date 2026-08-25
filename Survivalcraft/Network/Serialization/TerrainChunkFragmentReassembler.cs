using Game.Terrains.Distribution;

namespace Game.Network.Serialization;

public sealed class TerrainChunkFragmentReassembler
{
    private const int _maximumCompletedTransfers = 4096;
    private readonly Dictionary<TransferKey, Assembly> _assemblies = [];
    private readonly HashSet<TransferKey> _completed = [];
    private readonly Queue<TransferKey> _completedOrder = [];
    private readonly Lock _lock = new();

    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _assemblies.Count;
            }
        }
    }

    public bool Add(EncodedTerrainChunkFragment fragment, out EncodedTerrainChunk encoded)
    {
        lock (_lock)
        {
            Validate(fragment);
            var key = new TransferKey(fragment.Allocation, fragment.ContentVersion);
            if (_completed.Contains(key))
            {
                encoded = null!;
                return false;
            }

            foreach (var obsolete in _assemblies.Keys.Where(candidate =>
                         candidate.Allocation == fragment.Allocation && candidate != key).ToArray())
            {
                _assemblies.Remove(obsolete);
            }
            foreach (var obsolete in _completed.Where(candidate =>
                         candidate.Allocation == fragment.Allocation && candidate != key).ToArray())
            {
                _completed.Remove(obsolete);
            }

            if (!_assemblies.TryGetValue(key, out var assembly) ||
                assembly.TotalLength != fragment.TotalLength ||
                assembly.Fragments.Length != fragment.FragmentCount)
            {
                assembly = new Assembly(fragment.TotalLength, fragment.FragmentCount);
                _assemblies[key] = assembly;
            }

            if (assembly.Fragments[fragment.FragmentIndex] is null)
            {
                assembly.Fragments[fragment.FragmentIndex] = fragment.Payload;
                assembly.ReceivedCount++;
                assembly.ReceivedLength += fragment.Payload.Length;
            }

            if (assembly.ReceivedCount != assembly.Fragments.Length)
            {
                encoded = null!;
                return false;
            }

            if (assembly.ReceivedLength != assembly.TotalLength)
            {
                _assemblies.Remove(key);
                throw new InvalidDataException(
                    $"Terrain fragment length mismatch: {assembly.ReceivedLength}/{assembly.TotalLength}.");
            }

            var payload = new byte[assembly.TotalLength];
            var offset = 0;
            foreach (var part in assembly.Fragments)
            {
                part!.CopyTo(payload, offset);
                offset += part.Length;
            }

            _assemblies.Remove(key);
            _completed.Add(key);
            _completedOrder.Enqueue(key);
            while (_completedOrder.Count > _maximumCompletedTransfers)
            {
                _completed.Remove(_completedOrder.Dequeue());
            }
            encoded = new EncodedTerrainChunk(fragment.Allocation.Coords, fragment.ContentVersion, payload);
            return true;
        }
    }

    public bool TryCreateMissingFragmentRequest(
        ChunkAllocationId allocation,
        out TerrainChunkFragmentRequest request)
    {
        lock (_lock)
        {
            var entry = _assemblies.FirstOrDefault(item => item.Key.Allocation == allocation);
            if (entry.Value is null)
            {
                request = default;
                return false;
            }

            var missing = entry.Value.Fragments
                .Select((payload, index) => (payload, index))
                .Where(item => item.payload is null)
                .Select(item => checked((ushort)item.index))
                .ToArray();
            if (missing.Length == 0)
            {
                request = default;
                return false;
            }

            request = new TerrainChunkFragmentRequest(
                allocation,
                entry.Key.ContentVersion,
                checked((ushort)entry.Value.Fragments.Length),
                missing);
            return true;
        }
    }

    public int DiscardOtherGenerations(IEnumerable<ChunkAllocationId> allocations)
    {
        lock (_lock)
        {
            var current = new Dictionary<Point2, ChunkAllocationId>();
            foreach (var allocation in allocations)
            {
                current[allocation.Coords] = allocation;
            }
            var obsolete = _assemblies.Keys.Where(key =>
                    current.TryGetValue(key.Allocation.Coords, out var allocation) &&
                    allocation != key.Allocation)
                .ToArray();
            foreach (var key in obsolete)
            {
                _assemblies.Remove(key);
            }

            foreach (var key in _completed.Where(key =>
                         current.TryGetValue(key.Allocation.Coords, out var allocation) &&
                         allocation != key.Allocation).ToArray())
            {
                _completed.Remove(key);
            }

            return obsolete.Length;
        }
    }

    private static void Validate(EncodedTerrainChunkFragment fragment)
    {
        var expectedFragmentCount = Math.Max(
            1,
            (fragment.TotalLength + EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize - 1) /
            EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize);
        if (fragment.TotalLength is < 0 or > EncodedTerrainChunkFragmenter.MaximumPayloadLength ||
            fragment.FragmentCount is 0 or > EncodedTerrainChunkFragmenter.MaximumFragmentCount ||
            fragment.FragmentCount != expectedFragmentCount ||
            fragment.FragmentIndex >= fragment.FragmentCount ||
            fragment.Payload is null ||
            fragment.Payload.Length > EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize)
        {
            throw new InvalidDataException("Invalid terrain chunk fragment metadata.");
        }
    }

    private readonly record struct TransferKey(ChunkAllocationId Allocation, long ContentVersion);

    private sealed class Assembly(int totalLength, ushort fragmentCount)
    {
        public byte[][] Fragments { get; } = new byte[fragmentCount][];
        public int ReceivedCount;
        public int ReceivedLength;
        public int TotalLength { get; } = totalLength;
    }
}
