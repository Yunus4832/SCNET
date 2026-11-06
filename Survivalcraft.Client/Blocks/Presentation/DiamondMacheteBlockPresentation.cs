using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondMacheteBlockPresentation(DiamondMacheteBlock block, BlockPresentationCatalog catalog)
    : MacheteBlockPresentation(block, catalog, 47, 182)
{
    protected new DiamondMacheteBlock Block => (DiamondMacheteBlock)base.Block;
}
