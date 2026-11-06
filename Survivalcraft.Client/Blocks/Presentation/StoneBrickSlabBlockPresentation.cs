using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneBrickSlabBlockPresentation(StoneBrickSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 50)
{
    protected new StoneBrickSlabBlock Block => (StoneBrickSlabBlock)base.Block;
}
