using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronAxeBlockPresentation(IronAxeBlock block, BlockPresentationCatalog catalog)
    : AxeBlockPresentation(block, catalog, 47, 63)
{
    protected new IronAxeBlock Block => (IronAxeBlock)base.Block;
}
