using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CobblestoneSlabBlockPresentation(CobblestoneSlabBlock block, BlockPresentationCatalog catalog)
    : SlabBlockPresentation(block, catalog, 69)
{
    protected new CobblestoneSlabBlock Block => (CobblestoneSlabBlock)base.Block;
}
