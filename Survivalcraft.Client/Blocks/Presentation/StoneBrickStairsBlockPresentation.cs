using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneBrickStairsBlockPresentation(StoneBrickStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 50)
{
    protected new StoneBrickStairsBlock Block => (StoneBrickStairsBlock)base.Block;
}
