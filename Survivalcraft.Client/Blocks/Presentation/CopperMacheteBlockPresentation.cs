using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CopperMacheteBlockPresentation(CopperMacheteBlock block, BlockPresentationCatalog catalog)
    : MacheteBlockPresentation(block, catalog, 47, 79)
{
    protected new CopperMacheteBlock Block => (CopperMacheteBlock)base.Block;
}
