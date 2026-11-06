using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class TruthTableCircuitBlockPresentation(TruthTableCircuitBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new TruthTableCircuitBlock Block => (TruthTableCircuitBlock)base.Block;
}
