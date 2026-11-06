using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RandomGeneratorBlockPresentation(RandomGeneratorBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new RandomGeneratorBlock Block => (RandomGeneratorBlock)base.Block;
}
