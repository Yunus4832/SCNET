using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SandBlockPresentation(SandBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new SandBlock Block => (SandBlock)base.Block;
}
