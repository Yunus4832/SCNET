using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class PumpkinBlockPresentation(PumpkinBlock block, BlockPresentationCatalog catalog)
    : BasePumpkinBlockPresentation(block, catalog)
{
    protected new PumpkinBlock Block => (PumpkinBlock)base.Block;
}
