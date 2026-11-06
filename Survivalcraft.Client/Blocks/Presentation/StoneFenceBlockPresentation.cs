using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneFenceBlockPresentation(StoneFenceBlock block, BlockPresentationCatalog catalog)
    : FenceBlockPresentation(
        block,
        catalog,
        "Models/StoneFence",
        false,
        false,
        24,
        new Color(212, 212, 212),
        Color.White
    )
{
    protected new StoneFenceBlock Block => (StoneFenceBlock)base.Block;
}
