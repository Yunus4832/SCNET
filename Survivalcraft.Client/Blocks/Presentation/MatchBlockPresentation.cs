using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MatchBlockPresentation(MatchBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new MatchBlock Block => (MatchBlock)base.Block;
}
