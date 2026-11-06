using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FeatherBlockPresentation(FeatherBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new FeatherBlock Block => (FeatherBlock)base.Block;
}
