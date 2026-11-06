using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenFenceBlockPresentation(WoodenFenceBlock block, BlockPresentationCatalog catalog)
    : FenceBlockPresentation(block, catalog, "Models/WoodenFence", false, false, 23, Color.White, Color.White)
{
    protected new WoodenFenceBlock Block => (WoodenFenceBlock)base.Block;
}
