using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class ClayBlockPresentation(ClayBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 15)
{
    protected new ClayBlock Block => (ClayBlock)base.Block;
}
