using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronRakeBlockPresentation(IronRakeBlock block, BlockPresentationCatalog catalog)
    : RakeBlockPresentation(block, catalog, 47, 63)
{
    protected new IronRakeBlock Block => (IronRakeBlock)base.Block;
}
