using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenTrapdoorBlockPresentation(WoodenTrapdoorBlock block, BlockPresentationCatalog catalog)
    : TrapdoorBlockPresentation(block, catalog, "Models/WoodenTrapdoor")
{
    protected new WoodenTrapdoorBlock Block => (WoodenTrapdoorBlock)base.Block;
}
