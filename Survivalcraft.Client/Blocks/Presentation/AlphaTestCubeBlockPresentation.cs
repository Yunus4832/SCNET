using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class AlphaTestCubeBlockPresentation(AlphaTestCubeBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new AlphaTestCubeBlock Block => (AlphaTestCubeBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.AlphaTestSubsetsByFace);
    }
}
