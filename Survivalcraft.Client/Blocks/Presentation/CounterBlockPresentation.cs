using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CounterBlockPresentation(CounterBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new CounterBlock Block => (CounterBlock)base.Block;
}
