using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronFenceBlockPresentation(IronFenceBlock block, BlockPresentationCatalog catalog)
    : FenceBlockPresentation(
        block,
        catalog,
        "Models/IronFence",
        true,
        true,
        58,
        new Color(192, 192, 192),
        new Color(80, 80, 80)
    )
{
    protected new IronFenceBlock Block => (IronFenceBlock)base.Block;
}
