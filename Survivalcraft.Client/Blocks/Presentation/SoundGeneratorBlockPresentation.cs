using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SoundGeneratorBlockPresentation(SoundGeneratorBlock block, BlockPresentationCatalog catalog)
    : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new SoundGeneratorBlock Block => (SoundGeneratorBlock)base.Block;
}
