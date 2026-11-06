namespace Game.Terrains;

/// <summary>Optional world-owned derived CPU work executed under the terrain update token.</summary>
public interface ITerrainDerivedWork
{
    TerrainChunk? SelectChunk(TerrainChunk[] chunks, IEnumerable<TerrainUpdateLocation> locations);

    TerrainChunk? GetCpuDependency(TerrainChunk chunk);

    void UpdateChunk(TerrainChunk chunk);
}
