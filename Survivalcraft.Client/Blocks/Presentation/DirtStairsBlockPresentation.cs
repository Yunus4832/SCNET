using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DirtStairsBlockPresentation(DirtStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 2)
{
    protected new DirtStairsBlock Block => (DirtStairsBlock)base.Block;
}
