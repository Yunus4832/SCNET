using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class PoplarWoodBlockPresentation(PoplarWoodBlock block, BlockPresentationCatalog catalog)
    : WoodBlockPresentation(block, catalog, 21, 109)
{
    protected new PoplarWoodBlock Block => (PoplarWoodBlock)base.Block;
}
