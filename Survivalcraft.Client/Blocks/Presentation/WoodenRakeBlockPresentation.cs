using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenRakeBlockPresentation(WoodenRakeBlock block, BlockPresentationCatalog catalog)
    : RakeBlockPresentation(block, catalog, 47, 4)
{
    protected new WoodenRakeBlock Block => (WoodenRakeBlock)base.Block;
}
