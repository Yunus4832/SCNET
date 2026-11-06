using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class GravelBlockPresentation(GravelBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new GravelBlock Block => (GravelBlock)base.Block;
}
