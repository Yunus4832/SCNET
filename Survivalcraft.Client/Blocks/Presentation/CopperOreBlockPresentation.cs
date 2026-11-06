using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperOreBlockPresentation(CopperOreBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new CopperOreBlock Block => (CopperOreBlock)base.Block;
}
