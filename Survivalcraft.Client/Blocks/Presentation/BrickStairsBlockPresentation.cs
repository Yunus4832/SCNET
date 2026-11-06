using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BrickStairsBlockPresentation(BrickStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 39)
{
    protected new BrickStairsBlock Block => (BrickStairsBlock)base.Block;
}
