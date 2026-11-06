using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WireThroughCobblestoneBlockPresentation(
    WireThroughCobblestoneBlock block,
    BlockPresentationCatalog catalog
) : WireThroughBlockPresentation(block, catalog, 136, 16)
{
    protected new WireThroughCobblestoneBlock Block => (WireThroughCobblestoneBlock)base.Block;
}
