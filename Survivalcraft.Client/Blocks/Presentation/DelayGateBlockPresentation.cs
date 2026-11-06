using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DelayGateBlockPresentation(DelayGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new DelayGateBlock Block => (DelayGateBlock)base.Block;
}
