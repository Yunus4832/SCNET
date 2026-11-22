using System.Runtime.CompilerServices;

using Game.Terrains.Lighting;

namespace Game.Terrains.Rendering;

/// <summary>按相邻区块偏移索引网格采样光照使用的分段掩码。</summary>
internal struct TerrainMeshLightReads
{
    [InlineArray(9)]
    private struct NeighborMasks
    {
        private ushort _mask;
    }

    private NeighborMasks _neighbors;

    private NeighborMasks _interiorNeighbors;

    private Dictionary<Point2, ushort>? _outsideNeighbors;

    internal bool HasOutsideNeighbors => _outsideNeighbors != null;

    internal bool ReadsNeighbor(int index) => _neighbors[index] != 0;

    internal void Add(Point2 offset, int section, bool interior)
    {
        var mask = (ushort)(1 << section);
        if (offset.X is >= -1 and <= 1 && offset.Y is >= -1 and <= 1)
        {
            var index = GetIndex(offset);
            _neighbors[index] |= mask;
            if (interior) { _interiorNeighbors[index] |= mask; }
        }
        else
        {
            _outsideNeighbors ??= [];
            _outsideNeighbors[offset] = (ushort)(_outsideNeighbors.GetValueOrDefault(offset) | mask);
        }
    }

    internal bool Intersects(Point2 offset, ushort sections, TerrainLightBoundaryChanges? boundaries)
    {
        if (offset.X is < -1 or > 1 || offset.Y is < -1 or > 1)
        {
            return ((_outsideNeighbors?.GetValueOrDefault(offset) ?? 0) & sections) != 0;
        }

        var index = GetIndex(offset);
        if (boundaries == null || offset == default) { return (_neighbors[index] & sections) != 0; }

        return (_neighbors[index] & boundaries.Value.ForSampledNeighbor(offset)) != 0 ||
               (_interiorNeighbors[index] & sections) != 0;
    }

    internal void Include(in TerrainMeshLightReads reads)
    {
        for (var i = 0; i < 9; i++)
        {
            _neighbors[i] |= reads._neighbors[i];
            _interiorNeighbors[i] |= reads._interiorNeighbors[i];
        }

        if (reads._outsideNeighbors == null) { return; }

        _outsideNeighbors ??= [];
        foreach (var (offset, sections) in reads._outsideNeighbors)
        {
            _outsideNeighbors[offset] = (ushort)(_outsideNeighbors.GetValueOrDefault(offset) | sections);
        }
    }

    internal ulong Fingerprint(TerrainLightSnapshot?[] snapshots)
    {
        var fingerprint = 14695981039346656037UL;
        for (var i = 0; i < 9; i++) { fingerprint = AppendSampledSections(fingerprint, i, snapshots[i]); }

        return fingerprint;
    }

    internal ulong FingerprintCurrent(TerrainChunk chunk)
    {
        var fingerprint = 14695981039346656037UL;
        for (var i = 0; i < 9; i++)
        {
            if (!ReadsNeighbor(i)) { continue; }

            var neighbor = chunk.Terrain.GetChunkAtCoords(chunk.Coords.X + i / 3 - 1, chunk.Coords.Y + i % 3 - 1);
            if (neighbor != null && chunk.Terrain.Lighting.TryGetPublishedSnapshot(neighbor, out var snapshot))
            {
                fingerprint = AppendSampledSections(fingerprint, i, snapshot);
            }
        }

        return fingerprint;
    }

    private ulong AppendSampledSections(ulong fingerprint, int index, TerrainLightSnapshot? snapshot)
    {
        if (snapshot == null) { return fingerprint; }

        var remaining = (uint)_neighbors[index];
        while (remaining != 0)
        {
            var slice = System.Numerics.BitOperations.TrailingZeroCount(remaining);
            remaining &= remaining - 1;
            fingerprint = (fingerprint ^ (uint)(index * 16 + slice)) * 1099511628211UL;
            fingerprint = (fingerprint ^ snapshot.GetSection(slice).Fingerprint) * 1099511628211UL;
        }

        return fingerprint;
    }

    internal static TerrainMeshLightReads AroundSlice(int slice)
    {
        var first = Math.Max(0, slice - 1);
        var last = Math.Min(TerrainChunk.SlicesCount - 1, slice + 1);
        var mask = (ushort)(((1 << (last - first + 1)) - 1) << first);
        var result = new TerrainMeshLightReads();
        for (var i = 0; i < 9; i++) { result._neighbors[i] = mask; }

        return result;
    }

    private static int GetIndex(Point2 offset) => (offset.X + 1) * 3 + offset.Y + 1;
}
