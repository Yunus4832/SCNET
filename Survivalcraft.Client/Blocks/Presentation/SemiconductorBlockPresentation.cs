using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SemiconductorBlockPresentation(SemiconductorBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new SemiconductorBlock Block => (SemiconductorBlock)base.Block;
}
