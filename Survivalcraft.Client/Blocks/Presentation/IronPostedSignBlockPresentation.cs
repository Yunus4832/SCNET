using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronPostedSignBlockPresentation(IronPostedSignBlock block, BlockPresentationCatalog catalog)
    : PostedSignBlockPresentation(block, catalog, "Models/IronSign", 63)
{
    protected new IronPostedSignBlock Block => (IronPostedSignBlock)base.Block;
}
