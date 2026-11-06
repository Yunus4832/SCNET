using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StringBlockPresentation(StringBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new StringBlock Block => (StringBlock)base.Block;
}
