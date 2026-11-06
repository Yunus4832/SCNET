using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronOreBlockPresentation(IronOreBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new IronOreBlock Block => (IronOreBlock)base.Block;
}
