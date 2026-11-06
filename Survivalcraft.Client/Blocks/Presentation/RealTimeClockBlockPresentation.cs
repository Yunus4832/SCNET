using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RealTimeClockBlockPresentation(RealTimeClockBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new RealTimeClockBlock Block => (RealTimeClockBlock)base.Block;
}
