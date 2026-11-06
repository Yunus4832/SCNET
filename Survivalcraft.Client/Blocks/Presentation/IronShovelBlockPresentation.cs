using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronShovelBlockPresentation(IronShovelBlock block, BlockPresentationCatalog catalog)
    : ShovelBlockPresentation(block, catalog, 47, 63)
{
    protected new IronShovelBlock Block => (IronShovelBlock)base.Block;
}
