using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MarbleSlabBlockPresentation(MarbleSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 51)
{
    protected new MarbleSlabBlock Block => (MarbleSlabBlock)base.Block;
}
