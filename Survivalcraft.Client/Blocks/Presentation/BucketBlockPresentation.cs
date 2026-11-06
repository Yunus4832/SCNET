using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class BucketBlockPresentation(BucketBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new BucketBlock Block => (BucketBlock)base.Block;

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
    }
}
