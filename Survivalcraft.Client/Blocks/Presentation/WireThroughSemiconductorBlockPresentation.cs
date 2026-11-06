using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WireThroughSemiconductorBlockPresentation(
    WireThroughSemiconductorBlock block,
    BlockPresentationCatalog catalog
) : WireThroughBlockPresentation(block, catalog, 216, 78)
{
    protected new WireThroughSemiconductorBlock Block => (WireThroughSemiconductorBlock)base.Block;
}
