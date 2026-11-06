namespace Game.TerrainSerializers;

internal static class SeedTerrainGenerationPolicy
{
    public static int GetParallelism(int processorCount)
    {
        return Math.Clamp(processorCount - 1, 1, 4);
    }
}

/// <summary>
///     Owns seed-generated Pass1-Pass3 terrain data until the real chunk can adopt it.
/// </summary>
internal sealed class SeedGeneratedChunkBasis(int[] cells, long[] shafts)
{
    private readonly Lock _lock = new();

    private int[]? _cells = cells;

    private long[]? _shafts = shafts;

    public bool TryMoveTo(TerrainChunk chunk)
    {
        lock (_lock)
        {
            if (_cells == null || _shafts == null)
            {
                return false;
            }

            chunk.Cells = _cells;
            chunk.Shafts = _shafts;
            _cells = null;
            _shafts = null;
            return true;
        }
    }
}
