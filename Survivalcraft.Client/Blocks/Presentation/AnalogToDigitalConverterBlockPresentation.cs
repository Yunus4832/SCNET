using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class AnalogToDigitalConverterBlockPresentation(
    AnalogToDigitalConverterBlock block,
    BlockPresentationCatalog catalog
) : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new AnalogToDigitalConverterBlock Block => (AnalogToDigitalConverterBlock)base.Block;
}
