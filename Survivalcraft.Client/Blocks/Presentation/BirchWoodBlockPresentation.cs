using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BirchWoodBlockPresentation(BirchWoodBlock block, BlockPresentationCatalog catalog)
    : WoodBlockPresentation(block, catalog, 21, 117)
{
    protected new BirchWoodBlock Block => (BirchWoodBlock)base.Block;
}
