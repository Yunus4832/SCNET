using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MemoryBankBlockPresentation(MemoryBankBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new MemoryBankBlock Block => (MemoryBankBlock)base.Block;
}
