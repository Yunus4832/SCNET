using Engine.Graphics;

namespace Game.Terrains;

public class TerrainGeometry
{
    private readonly Dictionary<Texture2D, TerrainGeometry[]> _draws = new();

    private readonly int _slice;

    public TerrainGeometrySubset[] AlphaTestSubsetsByFace = [];

    public TerrainGeometrySubset[] OpaqueSubsetsByFace = [];

    public TerrainGeometrySubset SubsetAlphaTest = new();

    public TerrainGeometrySubset SubsetOpaque = new();

    public TerrainGeometrySubset[] Subsets = [];

    public TerrainGeometrySubset SubsetTransparent = new();

    public TerrainGeometrySubset[] TransparentSubsetsByFace = [];

    public TerrainGeometry(Dictionary<Texture2D, TerrainGeometry[]> draws, int slice = 0)
    {
        InitSubsets();
        _draws = draws;
        _slice = slice;
    }

    public TerrainGeometry()
    {
        InitSubsets();
    }

    protected void InitSubsets()
    {
        Subsets = new TerrainGeometrySubset[7];
        for (var i = 0; i < 7; i++)
        {
            Subsets[i] = new TerrainGeometrySubset();
        }

        SubsetOpaque = Subsets[4];
        SubsetAlphaTest = Subsets[5];
        SubsetTransparent = Subsets[6];
        OpaqueSubsetsByFace =
        [
            Subsets[0],
            Subsets[1],
            Subsets[2],
            Subsets[3],
            Subsets[4],
            Subsets[4]
        ];
        AlphaTestSubsetsByFace =
        [
            Subsets[5],
            Subsets[5],
            Subsets[5],
            Subsets[5],
            Subsets[5],
            Subsets[5]
        ];
        TransparentSubsetsByFace =
        [
            Subsets[6],
            Subsets[6],
            Subsets[6],
            Subsets[6],
            Subsets[6],
            Subsets[6]
        ];
    }

    public TerrainGeometry GetGeometry(Texture2D texture)
    {
        if (_draws.TryGetValue(texture, out var geometries))
        {
            return geometries[_slice];
        }

        geometries = new TerrainGeometry[32];
        for (var i = 0; i < 32; i++)
        {
            var t = new TerrainGeometry(_draws, i);
            geometries[i] = t;
        }

        _draws.Add(texture, geometries);

        return geometries[_slice];
    }

    public void Dispose()
    {
        foreach (var subset in Subsets)
        {
            subset.Dispose();
        }
    }
}
