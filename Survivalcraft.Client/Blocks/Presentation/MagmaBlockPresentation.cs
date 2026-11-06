using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MagmaBlockPresentation(MagmaBlock block, BlockPresentationCatalog catalog)
    : FluidBlockPresentation(block, catalog)
{
    protected new MagmaBlock Block => (MagmaBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        GenerateFluidTerrainVertices(
            generator,
            value,
            x,
            y,
            z,
            Color.White,
            Color.White,
            geometry.OpaqueSubsetsByFace
        );
    }
}
