namespace Game.TerrainSerializers;

public interface ITerrainContentsGenerator
{
    Vector3 FindCoarseSpawnPosition();

    void GenerateChunkContentsPass1(TerrainChunk chunk);

    void GenerateChunkContentsPass2(TerrainChunk chunk);

    void GenerateChunkContentsPass3(TerrainChunk chunk);

    void GenerateChunkContentsPass4(TerrainChunk chunk);

    bool TryTakeSeedGeneratedChunkBasis(TerrainChunk chunk)
    {
        return false;
    }
}
