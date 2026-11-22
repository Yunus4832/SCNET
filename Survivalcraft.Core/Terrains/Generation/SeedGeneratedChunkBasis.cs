namespace Game.Terrains.Generation;

internal static class SeedTerrainGenerationPolicy
{
    public static int GetParallelism(int processorCount)
    {
        return Math.Clamp(processorCount - 1, 1, 4);
    }
}

/// <summary>持有按种子生成的前三阶段数据，直到实际区块接管。</summary>
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
