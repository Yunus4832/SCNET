using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronBlockPresentation(IronBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new IronBlock Block => (IronBlock)base.Block;
}
