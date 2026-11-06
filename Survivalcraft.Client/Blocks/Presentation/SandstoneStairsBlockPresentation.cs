using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SandstoneStairsBlockPresentation(SandstoneStairsBlock block, BlockPresentationCatalog catalog)
    : StairsBlockPresentation(block, catalog, 64)
{
    protected new SandstoneStairsBlock Block => (SandstoneStairsBlock)base.Block;
}
