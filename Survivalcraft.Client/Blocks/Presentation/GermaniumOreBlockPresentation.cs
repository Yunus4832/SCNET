using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class GermaniumOreBlockPresentation(GermaniumOreBlock block, BlockPresentationCatalog catalog)
    : CubeBlockPresentation(block, catalog)
{
    protected new GermaniumOreBlock Block => (GermaniumOreBlock)base.Block;
}
