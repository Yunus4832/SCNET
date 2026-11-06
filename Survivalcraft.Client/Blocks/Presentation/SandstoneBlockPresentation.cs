using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SandstoneBlockPresentation(SandstoneBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 64)
{
    protected new SandstoneBlock Block => (SandstoneBlock)base.Block;
}
