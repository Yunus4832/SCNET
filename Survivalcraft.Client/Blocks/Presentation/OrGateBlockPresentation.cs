using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class OrGateBlockPresentation(OrGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new OrGateBlock Block => (OrGateBlock)base.Block;
}
