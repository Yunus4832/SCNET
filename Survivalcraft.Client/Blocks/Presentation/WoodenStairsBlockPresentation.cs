using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenStairsBlockPresentation(WoodenStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 23)
{
    protected new WoodenStairsBlock Block => (WoodenStairsBlock)base.Block;
}
