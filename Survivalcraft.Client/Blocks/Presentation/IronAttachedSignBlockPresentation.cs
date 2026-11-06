using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronAttachedSignBlockPresentation(IronAttachedSignBlock block, BlockPresentationCatalog catalog)
    : AttachedSignBlockPresentation(block, catalog, "Models/IronSign", 63)
{
    protected new IronAttachedSignBlock Block => (IronAttachedSignBlock)base.Block;
}
