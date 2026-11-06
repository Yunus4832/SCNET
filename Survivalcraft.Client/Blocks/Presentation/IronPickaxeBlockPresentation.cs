using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronPickaxeBlockPresentation(IronPickaxeBlock block, BlockPresentationCatalog catalog)
    : PickaxeBlockPresentation(block, catalog, 47, 63)
{
    protected new IronPickaxeBlock Block => (IronPickaxeBlock)base.Block;
}
