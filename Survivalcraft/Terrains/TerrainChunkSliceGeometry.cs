namespace Game.Terrains;

public class TerrainChunkSliceGeometry : TerrainGeometry
{
    public int ContentsHash;

    public TerrainChunkSliceGeometry()
    {
        InitSubsets();
    }
}
