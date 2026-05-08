namespace Game.Blocks;

public class WaterBlock() : FluidBlock(WaterMaxLevel)
{
    public const int Index = 18;

    public const int WaterMaxLevel = 7;

    public override bool FurnitureBuilt { get; set; } = true;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        Color sideColor;
        var color = sideColor = BlockColorsMap.WaterColorsMap.Lookup(generator.Terrain, x, y, z);
        sideColor.A = byte.MaxValue;
        var topColor = color;
        topColor.A = 0;
        GenerateFluidTerrainVertices(generator, value, x, y, z, sideColor, topColor, geometry.TransparentSubsetsByFace);
    }
}
