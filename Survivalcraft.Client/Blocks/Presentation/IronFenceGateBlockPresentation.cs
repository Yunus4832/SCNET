using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronFenceGateBlockPresentation(IronFenceGateBlock block, BlockPresentationCatalog catalog)
    : FenceGateBlockPresentation(
        block,
        catalog,
        "Models/IronFenceGate",
        true,
        true,
        58,
        new Color(192, 192, 192),
        new Color(80, 80, 80)
    )
{
    protected new IronFenceGateBlock Block => (IronFenceGateBlock)base.Block;
}
