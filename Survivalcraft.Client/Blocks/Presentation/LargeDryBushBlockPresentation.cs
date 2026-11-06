using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class LargeDryBushBlockPresentation(LargeDryBushBlock block, BlockPresentationCatalog catalog)
    : CrossBlockPresentation(block, catalog)
{
    protected new LargeDryBushBlock Block => (LargeDryBushBlock)base.Block;
}
