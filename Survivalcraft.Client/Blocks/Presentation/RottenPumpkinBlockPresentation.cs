using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RottenPumpkinBlockPresentation(RottenPumpkinBlock block, BlockPresentationCatalog catalog)
    : BasePumpkinBlockPresentation(block, catalog)
{
    protected new RottenPumpkinBlock Block => (RottenPumpkinBlock)base.Block;
}
