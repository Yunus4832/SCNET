using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondSpearBlockPresentation(DiamondSpearBlock block, BlockPresentationCatalog catalog)
    : SpearBlockPresentation(block, catalog, 47, 182)
{
    protected new DiamondSpearBlock Block => (DiamondSpearBlock)base.Block;
}
