using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CobblestoneBlockPresentation(CobblestoneBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 69)
{
    protected new CobblestoneBlock Block => (CobblestoneBlock)base.Block;
}
