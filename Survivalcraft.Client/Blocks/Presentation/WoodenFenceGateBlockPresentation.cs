using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenFenceGateBlockPresentation(WoodenFenceGateBlock block, BlockPresentationCatalog catalog)
    : FenceGateBlockPresentation(block, catalog, "Models/WoodenFenceGate", false, false, 23, Color.White, Color.White)
{
    protected new WoodenFenceGateBlock Block => (WoodenFenceGateBlock)base.Block;
}
