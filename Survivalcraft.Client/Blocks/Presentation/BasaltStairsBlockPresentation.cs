using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BasaltStairsBlockPresentation(BasaltStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 40)
{
    protected new BasaltStairsBlock Block => (BasaltStairsBlock)base.Block;
}
