using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SandstoneSlabBlockPresentation(SandstoneSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 64)
{
    protected new SandstoneSlabBlock Block => (SandstoneSlabBlock)base.Block;
}
