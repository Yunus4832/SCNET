using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MarbleStairsBlockPresentation(MarbleStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 51)
{
    protected new MarbleStairsBlock Block => (MarbleStairsBlock)base.Block;
}
