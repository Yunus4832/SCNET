using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondPickaxeBlockPresentation(DiamondPickaxeBlock block, BlockPresentationCatalog catalog)
    : PickaxeBlockPresentation(block, catalog, 47, 182)
{
    protected new DiamondPickaxeBlock Block => (DiamondPickaxeBlock)base.Block;
}
