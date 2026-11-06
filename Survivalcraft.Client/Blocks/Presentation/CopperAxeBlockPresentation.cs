using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperAxeBlockPresentation(CopperAxeBlock block, BlockPresentationCatalog catalog)
    : AxeBlockPresentation(block, catalog, 47, 79)
{
    protected new CopperAxeBlock Block => (CopperAxeBlock)base.Block;
}
