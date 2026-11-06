using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class LitFurnaceBlockPresentation(LitFurnaceBlock block, BlockPresentationCatalog catalog)
    : FurnaceBlockPresentation(block, catalog)
{
    protected new LitFurnaceBlock Block => (LitFurnaceBlock)base.Block;
}
