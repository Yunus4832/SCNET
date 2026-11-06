using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronLadderBlockPresentation(IronLadderBlock block, BlockPresentationCatalog catalog)
    : LadderBlockPresentation(block, catalog, "Models/IronLadder")
{
    protected new IronLadderBlock Block => (IronLadderBlock)base.Block;
}
