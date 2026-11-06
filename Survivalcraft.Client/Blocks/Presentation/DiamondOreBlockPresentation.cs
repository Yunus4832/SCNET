using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondOreBlockPresentation(DiamondOreBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new DiamondOreBlock Block => (DiamondOreBlock)base.Block;
}
