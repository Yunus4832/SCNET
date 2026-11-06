using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class OakWoodBlockPresentation(OakWoodBlock block, BlockPresentationCatalog catalog)
    : WoodBlockPresentation(block, catalog, 21, 20)
{
    protected new OakWoodBlock Block => (OakWoodBlock)base.Block;
}
