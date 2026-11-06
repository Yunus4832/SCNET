using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneSlabBlockPresentation(StoneSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 24)
{
    protected new StoneSlabBlock Block => (StoneSlabBlock)base.Block;
}
