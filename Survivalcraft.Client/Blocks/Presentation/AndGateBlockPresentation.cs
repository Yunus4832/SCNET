using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class AndGateBlockPresentation(AndGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new AndGateBlock Block => (AndGateBlock)base.Block;
}
