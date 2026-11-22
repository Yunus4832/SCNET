using System.Runtime.CompilerServices;

namespace Game.Terrains.Lighting;

/// <summary>权威光照快照各水平边缘及角落发生变化的分段。</summary>
public struct TerrainLightBoundaryChanges : IEquatable<TerrainLightBoundaryChanges>
{
    [InlineArray(9)]
    private struct SectionMasks
    {
        private ushort _mask;
    }

    private SectionMasks _masks;

    /// <summary>从网格到被采样光照区块的偏移；东侧区块向该网格提供西侧边缘。</summary>
    public readonly ushort ForSampledNeighbor(Point2 offset) => _masks[(offset.X + 1) * 3 + offset.Y + 1];

    public readonly bool Equals(TerrainLightBoundaryChanges other)
    {
        for (var i = 0; i < 9; i++)
        {
            if (_masks[i] != other._masks[i]) { return false; }
        }

        return true;
    }

    public override readonly bool Equals(object? obj) => obj is TerrainLightBoundaryChanges changes && Equals(changes);

    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        for (var i = 0; i < 9; i++) { hash.Add(_masks[i]); }

        return hash.ToHashCode();
    }

    internal void Include(TerrainLightSnapshot? previous, TerrainLightSnapshot current, ushort sections)
    {
        var remaining = (uint)sections;
        while (remaining != 0)
        {
            var slice = System.Numerics.BitOperations.TrailingZeroCount(remaining);
            remaining &= remaining - 1;
            var before = previous?.GetSection(slice);
            var after = current.GetSection(slice);
            if (ReferenceEquals(before, after)) { continue; }

            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    var dx = x == 0 ? 1 : x == 15 ? -1 : 0;
                    var dz = z == 0 ? 1 : z == 15 ? -1 : 0;
                    if (dx == 0 && dz == 0) { continue; }

                    var column = (x << 4) + (z << 8);
                    if (before?.HasSameColumn(after, column) == true) { continue; }

                    var mask = (ushort)(1 << slice);
                    if (dx != 0) { _masks[(dx + 1) * 3 + 1] |= mask; }

                    if (dz != 0) { _masks[3 + dz + 1] |= mask; }

                    if (dx != 0 && dz != 0) { _masks[(dx + 1) * 3 + dz + 1] |= mask; }
                }
            }
        }
    }
}
