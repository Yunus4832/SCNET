using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DirtBlockPresentation(DirtBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new DirtBlock Block => (DirtBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        generator.GenerateCubeVertices(
            this,
            value,
            x,
            y,
            z,
            Color.White,
            geometry.OpaqueSubsetsByFace
        );
    }
}
