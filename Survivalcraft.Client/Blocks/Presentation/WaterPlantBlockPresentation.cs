using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class WaterPlantBlockPresentation(WaterPlantBlock block, BlockPresentationCatalog catalog)
    : WaterBlockPresentation(block, catalog)
{
    protected new WaterPlantBlock Block => (WaterPlantBlock)base.Block;
}
