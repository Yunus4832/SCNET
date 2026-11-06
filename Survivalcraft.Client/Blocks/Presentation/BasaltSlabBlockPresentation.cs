using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BasaltSlabBlockPresentation(BasaltSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 40)
{
    protected new BasaltSlabBlock Block => (BasaltSlabBlock)base.Block;
}
