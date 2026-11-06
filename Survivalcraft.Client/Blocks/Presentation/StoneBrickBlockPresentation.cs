using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneBrickBlockPresentation(StoneBrickBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 50)
{
    protected new StoneBrickBlock Block => (StoneBrickBlock)base.Block;
}
