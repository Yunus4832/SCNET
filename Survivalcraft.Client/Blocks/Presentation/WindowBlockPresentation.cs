using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WindowBlockPresentation(WindowBlock block, BlockPresentationCatalog catalog)
    : AlphaTestCubeBlockPresentation(block, catalog)
{
    protected new WindowBlock Block => (WindowBlock)base.Block;
}
