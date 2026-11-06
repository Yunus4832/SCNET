using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class NotGateBlockPresentation(NotGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new NotGateBlock Block => (NotGateBlock)base.Block;
}
