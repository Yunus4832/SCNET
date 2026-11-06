using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenSpearBlockPresentation(WoodenSpearBlock block, BlockPresentationCatalog catalog)
    : SpearBlockPresentation(block, catalog, 47, 4)
{
    protected new WoodenSpearBlock Block => (WoodenSpearBlock)base.Block;
}
