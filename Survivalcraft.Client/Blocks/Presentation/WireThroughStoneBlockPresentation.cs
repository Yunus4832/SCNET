using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WireThroughStoneBlockPresentation(WireThroughStoneBlock block, BlockPresentationCatalog catalog)
    : WireThroughBlockPresentation(block, catalog, 184, 1)
{
    protected new WireThroughStoneBlock Block => (WireThroughStoneBlock)base.Block;
}
