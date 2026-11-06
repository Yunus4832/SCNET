using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SnowBlockPresentation(SnowBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new SnowBlock Block => (SnowBlock)base.Block;

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
            0.125f,
            0.125f,
            0.125f,
            0.125f,
            Color.White,
            Color.White,
            Color.White,
            Color.White,
            Color.White,
            -1,
            geometry.OpaqueSubsetsByFace
        );
    }
}
