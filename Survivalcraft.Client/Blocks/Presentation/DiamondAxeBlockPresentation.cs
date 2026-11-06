using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondAxeBlockPresentation(DiamondAxeBlock block, BlockPresentationCatalog catalog)
    : AxeBlockPresentation(block, catalog, 47, 182)
{
    protected new DiamondAxeBlock Block => (DiamondAxeBlock)base.Block;
}
