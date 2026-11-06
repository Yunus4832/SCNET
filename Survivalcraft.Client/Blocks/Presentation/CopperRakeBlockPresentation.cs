using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperRakeBlockPresentation(CopperRakeBlock block, BlockPresentationCatalog catalog)
    : RakeBlockPresentation(block, catalog, 47, 79)
{
    protected new CopperRakeBlock Block => (CopperRakeBlock)base.Block;
}
