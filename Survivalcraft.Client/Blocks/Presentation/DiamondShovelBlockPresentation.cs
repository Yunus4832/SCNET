using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DiamondShovelBlockPresentation(DiamondShovelBlock block, BlockPresentationCatalog catalog)
    : ShovelBlockPresentation(block, catalog, 47, 182)
{
    protected new DiamondShovelBlock Block => (DiamondShovelBlock)base.Block;
}
