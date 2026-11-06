using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DirtSlabBlockPresentation(DirtSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 2)
{
    protected new DirtSlabBlock Block => (DirtSlabBlock)base.Block;
}
