using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronMacheteBlockPresentation(IronMacheteBlock block, BlockPresentationCatalog catalog)
    : MacheteBlockPresentation(block, catalog, 47, 63)
{
    protected new IronMacheteBlock Block => (IronMacheteBlock)base.Block;
}
