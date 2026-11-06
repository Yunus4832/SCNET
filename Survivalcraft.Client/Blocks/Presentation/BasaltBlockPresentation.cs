using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BasaltBlockPresentation(BasaltBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 40)
{
    protected new BasaltBlock Block => (BasaltBlock)base.Block;
}
