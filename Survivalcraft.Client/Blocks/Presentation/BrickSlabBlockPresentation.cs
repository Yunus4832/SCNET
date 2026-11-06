using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BrickSlabBlockPresentation(BrickSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 39)
{
    protected new BrickSlabBlock Block => (BrickSlabBlock)base.Block;
}
