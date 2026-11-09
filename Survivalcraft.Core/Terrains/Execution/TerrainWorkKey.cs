namespace Game.Terrains.Execution;

public readonly record struct TerrainWorkKey(TerrainChunk Chunk, ulong Generation, TerrainWorkStage Stage)
{
    public TerrainWorkKey(TerrainChunk chunk, TerrainWorkStage stage) : this(chunk, chunk.AllocationGeneration, stage)
    {
    }

    public bool IsCurrent => !Chunk.IsDisposed && Chunk.AllocationGeneration == Generation;
}
