namespace Game.Terrains.Distribution;

/// <summary>
/// Owns client chunk allocation lifetimes and installed authoritative contents.
/// </summary>
public sealed class ClientChunkContentStore
{
    private readonly Dictionary<Point2, Entry> _active = [];

    private readonly Dictionary<Point2, ulong> _lastGenerations = [];

    public int Count => _active.Count;

    public ChunkAllocationId Allocate(Point2 coords)
    {
        if (_active.ContainsKey(coords))
        {
            throw new InvalidOperationException($"Chunk {coords} is already allocated.");
        }

        var generation = _lastGenerations.GetValueOrDefault(coords) + 1;
        _lastGenerations[coords] = generation;
        var allocation = new ChunkAllocationId(coords, generation);
        _active.Add(coords, new Entry(allocation));
        return allocation;
    }

    public bool Release(ChunkAllocationId allocation)
    {
        return _active.TryGetValue(allocation.Coords, out var entry) &&
               entry.Allocation == allocation &&
               _active.Remove(allocation.Coords);
    }

    public bool TryInstall(ClientChunkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!_active.TryGetValue(snapshot.Allocation.Coords, out var entry) ||
            entry.Allocation != snapshot.Allocation ||
            snapshot.ContentVersion <= entry.ContentVersion)
        {
            return false;
        }

        entry.ContentVersion = snapshot.ContentVersion;
        entry.Cells = snapshot.Cells.ToArray();
        entry.Shafts = snapshot.Shafts.ToArray();
        return true;
    }

    public bool TryGet(ChunkAllocationId allocation, out ClientChunkContent content)
    {
        if (_active.TryGetValue(allocation.Coords, out var entry) &&
            entry.Allocation == allocation &&
            entry is { Cells: not null, Shafts: not null })
        {
            content = new ClientChunkContent(
                allocation,
                entry.ContentVersion,
                entry.Cells,
                entry.Shafts);
            return true;
        }

        content = default;
        return false;
    }

    private sealed class Entry(ChunkAllocationId allocation)
    {
        public ChunkAllocationId Allocation { get; } = allocation;

        public long ContentVersion { get; set; }

        public int[]? Cells { get; set; }

        public long[]? Shafts { get; set; }
    }
}

public readonly record struct ClientChunkContent(
    ChunkAllocationId Allocation,
    long ContentVersion,
    ReadOnlyMemory<int> Cells,
    ReadOnlyMemory<long> Shafts);
