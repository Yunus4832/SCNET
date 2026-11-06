using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class XorGateBlockPresentation(XorGateBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new XorGateBlock Block => (XorGateBlock)base.Block;
}
