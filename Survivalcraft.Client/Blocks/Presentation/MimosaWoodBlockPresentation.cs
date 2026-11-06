using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MimosaWoodBlockPresentation(MimosaWoodBlock block, BlockPresentationCatalog catalog)
    : WoodBlockPresentation(block, catalog, 21, 116)
{
    protected new MimosaWoodBlock Block => (MimosaWoodBlock)base.Block;
}
