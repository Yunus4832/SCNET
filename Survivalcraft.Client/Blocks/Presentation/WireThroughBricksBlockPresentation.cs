using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WireThroughBricksBlockPresentation(WireThroughBricksBlock block, BlockPresentationCatalog catalog)
    : WireThroughBlockPresentation(block, catalog, 152, 70)
{
    protected new WireThroughBricksBlock Block => (WireThroughBricksBlock)base.Block;
}
