using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneStairsBlockPresentation(StoneStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 24)
{
    protected new StoneStairsBlock Block => (StoneStairsBlock)base.Block;
}
