namespace Game.Terrains;

/// <summary>Optional client-owned derived work, without owning CPU terrain selection or dependencies.</summary>
public interface ITerrainDerivedWork
{
    bool HasCurrentGeometry(TerrainChunk chunk);

    bool HasUploadedGeometry(TerrainChunk chunk);

    TerrainDerivedWorkResult UpdateChunk(TerrainChunk chunk);
}
