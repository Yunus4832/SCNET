using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class LimestoneBlockPresentation(LimestoneBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new LimestoneBlock Block => (LimestoneBlock)base.Block;
}
