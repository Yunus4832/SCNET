using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RedFlowerBlockPresentation(RedFlowerBlock block, BlockPresentationCatalog catalog)
    : FlowerBlockPresentation(block, catalog)
{
    protected new RedFlowerBlock Block => (RedFlowerBlock)base.Block;
}
