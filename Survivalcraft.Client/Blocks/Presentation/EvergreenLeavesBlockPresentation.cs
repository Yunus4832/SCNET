using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class EvergreenLeavesBlockPresentation(EvergreenLeavesBlock block, BlockPresentationCatalog catalog)
    : LeavesBlockPresentation(block, catalog)
{
    protected new EvergreenLeavesBlock Block => (EvergreenLeavesBlock)base.Block;
}
