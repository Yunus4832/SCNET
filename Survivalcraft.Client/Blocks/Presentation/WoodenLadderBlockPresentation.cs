using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenLadderBlockPresentation(WoodenLadderBlock block, BlockPresentationCatalog catalog)
    : LadderBlockPresentation(block, catalog, "Models/WoodenLadder")
{
    protected new WoodenLadderBlock Block => (WoodenLadderBlock)base.Block;
}
