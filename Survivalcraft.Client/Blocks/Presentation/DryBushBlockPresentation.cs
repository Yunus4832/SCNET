using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DryBushBlockPresentation(DryBushBlock block, BlockPresentationCatalog catalog)
    : CrossBlockPresentation(block, catalog)
{
    protected new DryBushBlock Block => (DryBushBlock)base.Block;
}
