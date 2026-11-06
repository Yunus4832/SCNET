using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenAttachedSignBlockPresentation(WoodenAttachedSignBlock block, BlockPresentationCatalog catalog)
    : AttachedSignBlockPresentation(block, catalog, "Models/WoodenSign", 23)
{
    protected new WoodenAttachedSignBlock Block => (WoodenAttachedSignBlock)base.Block;
}
