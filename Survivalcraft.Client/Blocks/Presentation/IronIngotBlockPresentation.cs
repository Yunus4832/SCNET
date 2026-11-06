using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronIngotBlockPresentation(IronIngotBlock block, BlockPresentationCatalog catalog)
    : IngotBlockPresentation(block, catalog, "IronIngot")
{
    protected new IronIngotBlock Block => (IronIngotBlock)base.Block;
}
