namespace Game.TerrainSerializers;

public interface ITerrainContentsGenerator
{
    Vector3 FindCoarseSpawnPosition();

    void GenerateChunkContentsPass1(TerrainChunk chunk);

    void GenerateChunkContentsPass2(TerrainChunk chunk);

    void GenerateChunkContentsPass3(TerrainChunk chunk);

    /// <summary>Applies final decorations after fourth-pass preparation has completed.</summary>
    void GenerateChunkContentsPass4(TerrainChunk chunk);

    /// <summary>Captures owned inputs before preparing immutable generation data outside terrain access.</summary>
    Task PrepareChunkContentsPass4Async(TerrainChunk chunk, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    bool TryTakeSeedGeneratedChunkBasis(TerrainChunk chunk)
    {
        return false;
    }
}
