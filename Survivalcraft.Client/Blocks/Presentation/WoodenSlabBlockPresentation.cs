using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenSlabBlockPresentation(WoodenSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 23)
{
    protected new WoodenSlabBlock Block => (WoodenSlabBlock)base.Block;
}
