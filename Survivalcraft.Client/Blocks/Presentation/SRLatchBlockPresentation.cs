using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SRLatchBlockPresentation(SRLatchBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new SRLatchBlock Block => (SRLatchBlock)base.Block;
}
