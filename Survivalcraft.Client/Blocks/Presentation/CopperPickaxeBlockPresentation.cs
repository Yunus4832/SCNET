using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperPickaxeBlockPresentation(CopperPickaxeBlock block, BlockPresentationCatalog catalog)
    : PickaxeBlockPresentation(block, catalog, 47, 79)
{
    protected new CopperPickaxeBlock Block => (CopperPickaxeBlock)base.Block;
}
