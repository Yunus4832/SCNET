using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondRakeBlockPresentation(DiamondRakeBlock block, BlockPresentationCatalog catalog)
    : RakeBlockPresentation(block, catalog, 47, 182)
{
    protected new DiamondRakeBlock Block => (DiamondRakeBlock)base.Block;
}
