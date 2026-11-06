using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperIngotBlockPresentation(CopperIngotBlock block, BlockPresentationCatalog catalog)
    : IngotBlockPresentation(block, catalog, "CopperIngot")
{
    protected new CopperIngotBlock Block => (CopperIngotBlock)base.Block;
}
