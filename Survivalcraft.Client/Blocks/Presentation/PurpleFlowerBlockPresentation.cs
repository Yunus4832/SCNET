using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class PurpleFlowerBlockPresentation(PurpleFlowerBlock block, BlockPresentationCatalog catalog)
    : FlowerBlockPresentation(block, catalog)
{
    protected new PurpleFlowerBlock Block => (PurpleFlowerBlock)base.Block;
}
