using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CoalBlockPresentation(CoalBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new CoalBlock Block => (CoalBlock)base.Block;
}
