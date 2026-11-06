using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperSpearBlockPresentation(CopperSpearBlock block, BlockPresentationCatalog catalog)
    : SpearBlockPresentation(block, catalog, 47, 79)
{
    protected new CopperSpearBlock Block => (CopperSpearBlock)base.Block;
}
