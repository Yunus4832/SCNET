using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DigitalToAnalogConverterBlockPresentation(
    DigitalToAnalogConverterBlock block,
    BlockPresentationCatalog catalog
) : RotatableMountedElectricElementBlockPresentation(block, catalog, "Models/Gates")
{
    protected new DigitalToAnalogConverterBlock Block => (DigitalToAnalogConverterBlock)base.Block;
}
