using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperBlockPresentation(CopperBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new CopperBlock Block => (CopperBlock)base.Block;
}
