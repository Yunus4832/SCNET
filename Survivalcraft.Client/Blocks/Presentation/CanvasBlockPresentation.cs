using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CanvasBlockPresentation(CanvasBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new CanvasBlock Block => (CanvasBlock)base.Block;
}
