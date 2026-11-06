using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IceBlockPresentation(IceBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new IceBlock Block => (IceBlock)base.Block;
}
