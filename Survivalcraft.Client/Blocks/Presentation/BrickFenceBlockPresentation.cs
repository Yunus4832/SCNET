using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BrickFenceBlockPresentation(BrickFenceBlock block, BlockPresentationCatalog catalog)
    : FenceBlockPresentation(
        block,
        catalog,
        "Models/StoneFence",
        false,
        false,
        39,
        new Color(212, 212, 212),
        Color.White
    )
{
    protected new BrickFenceBlock Block => (BrickFenceBlock)base.Block;
}
