using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CobblestoneStairsBlockPresentation(CobblestoneStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 69)
{
    protected new CobblestoneStairsBlock Block => (CobblestoneStairsBlock)base.Block;
}
