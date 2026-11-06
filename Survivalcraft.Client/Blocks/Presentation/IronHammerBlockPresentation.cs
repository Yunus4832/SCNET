using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronHammerBlockPresentation(IronHammerBlock block, BlockPresentationCatalog catalog)
    : HammerBlockPresentation(block, catalog, 47, 63)
{
    protected new IronHammerBlock Block => (IronHammerBlock)base.Block;
}
