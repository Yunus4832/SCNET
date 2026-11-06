using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class NorGateBlockPresentation(NorGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new NorGateBlock Block => (NorGateBlock)base.Block;
}
