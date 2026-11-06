using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class NandGateBlockPresentation(NandGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new NandGateBlock Block => (NandGateBlock)base.Block;
}
