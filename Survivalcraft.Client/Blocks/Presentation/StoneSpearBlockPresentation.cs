using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneSpearBlockPresentation(StoneSpearBlock block, BlockPresentationCatalog catalog)
    : SpearBlockPresentation(block, catalog, 47, 1)
{
    protected new StoneSpearBlock Block => (StoneSpearBlock)base.Block;
}
