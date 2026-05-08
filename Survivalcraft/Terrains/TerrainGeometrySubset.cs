namespace Game.Terrains;

public class TerrainGeometrySubset
{
    public readonly DynamicArray<int> Indices = [];

    public DynamicArray<TerrainVertex> Vertices = [];

    public TerrainGeometrySubset()
    {
    }

    public TerrainGeometrySubset(DynamicArray<TerrainVertex> vertices, DynamicArray<int> indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    public void Dispose()
    {
    }
}
