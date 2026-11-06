using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class GraniteBlockPresentation(GraniteBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 24)
{
    protected new GraniteBlock Block => (GraniteBlock)base.Block;
}
