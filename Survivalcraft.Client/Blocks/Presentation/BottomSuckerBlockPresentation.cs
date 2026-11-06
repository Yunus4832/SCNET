using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class BottomSuckerBlockPresentation(BottomSuckerBlock block, BlockPresentationCatalog catalog)
    : WaterBlockPresentation(block, catalog)
{
    protected new BottomSuckerBlock Block => (BottomSuckerBlock)base.Block;
}
