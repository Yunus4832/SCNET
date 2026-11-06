using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SpruceWoodBlockPresentation(SpruceWoodBlock block, BlockPresentationCatalog catalog)
    : WoodBlockPresentation(block, catalog, 21, 116)
{
    protected new SpruceWoodBlock Block => (SpruceWoodBlock)base.Block;
}
