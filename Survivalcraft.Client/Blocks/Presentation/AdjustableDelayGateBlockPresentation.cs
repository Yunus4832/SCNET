using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class AdjustableDelayGateBlockPresentation(AdjustableDelayGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new AdjustableDelayGateBlock Block => (AdjustableDelayGateBlock)base.Block;
}
