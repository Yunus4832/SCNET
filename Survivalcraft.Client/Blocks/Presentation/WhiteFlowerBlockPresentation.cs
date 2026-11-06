using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WhiteFlowerBlockPresentation(WhiteFlowerBlock block, BlockPresentationCatalog catalog)
    : FlowerBlockPresentation(block, catalog)
{
    protected new WhiteFlowerBlock Block => (WhiteFlowerBlock)base.Block;
}
