using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BasaltFenceBlockPresentation(BasaltFenceBlock block, BlockPresentationCatalog catalog)
    : FenceBlockPresentation(
        block,
        catalog,
        "Models/StoneFence",
        false,
        false,
        40,
        new Color(212, 212, 212),
        Color.White
    )
{
    protected new BasaltFenceBlock Block => (BasaltFenceBlock)base.Block;
}
