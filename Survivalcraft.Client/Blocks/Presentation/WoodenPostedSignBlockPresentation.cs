using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenPostedSignBlockPresentation(WoodenPostedSignBlock block, BlockPresentationCatalog catalog)
    : PostedSignBlockPresentation(block, catalog, "Models/WoodenSign", 23)
{
    protected new WoodenPostedSignBlock Block => (WoodenPostedSignBlock)base.Block;
}
