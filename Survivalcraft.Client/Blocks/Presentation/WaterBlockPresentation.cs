using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WaterBlockPresentation(WaterBlock block, BlockPresentationCatalog catalog)
    : FluidBlockPresentation(block, catalog)
{
    protected new WaterBlock Block => (WaterBlock)base.Block;

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
        GenerateFluidTerrainVertices(
            generator,
            value,
            x,
            y,
            z,
            sideColor,
            topColor,
            geometry.TransparentSubsetsByFace
        );
    }
}
