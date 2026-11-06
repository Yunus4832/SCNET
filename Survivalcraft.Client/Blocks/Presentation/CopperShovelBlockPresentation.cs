using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperShovelBlockPresentation(CopperShovelBlock block, BlockPresentationCatalog catalog)
    : ShovelBlockPresentation(block, catalog, 47, 79)
{
    protected new CopperShovelBlock Block => (CopperShovelBlock)base.Block;
}
