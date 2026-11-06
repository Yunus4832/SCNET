using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WireThroughPlanksBlockPresentation(WireThroughPlanksBlock block, BlockPresentationCatalog catalog)
    : WireThroughBlockPresentation(block, catalog, 168, 4)
{
    protected new WireThroughPlanksBlock Block => (WireThroughPlanksBlock)base.Block;
}
