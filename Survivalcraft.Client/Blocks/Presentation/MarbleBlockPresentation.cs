using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MarbleBlockPresentation(MarbleBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 51)
{
    protected new MarbleBlock Block => (MarbleBlock)base.Block;
}
