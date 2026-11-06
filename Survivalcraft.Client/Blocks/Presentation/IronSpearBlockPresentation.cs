using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronSpearBlockPresentation(IronSpearBlock block, BlockPresentationCatalog catalog)
    : SpearBlockPresentation(block, catalog, 47, 63)
{
    protected new IronSpearBlock Block => (IronSpearBlock)base.Block;
}
