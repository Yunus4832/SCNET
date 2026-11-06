using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CoalOreBlockPresentation(CoalOreBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new CoalOreBlock Block => (CoalOreBlock)base.Block;
}
