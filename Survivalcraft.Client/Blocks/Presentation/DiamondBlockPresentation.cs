using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondBlockPresentation(DiamondBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new DiamondBlock Block => (DiamondBlock)base.Block;
}
