using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BricksBlockPresentation(BricksBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 39)
{
    protected new BricksBlock Block => (BricksBlock)base.Block;
}
