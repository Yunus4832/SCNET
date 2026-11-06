using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class PlanksBlockPresentation(PlanksBlock block, BlockPresentationCatalog catalog)
    : PaintedCubeBlockPresentation(block, catalog, 23)
{
    protected new PlanksBlock Block => (PlanksBlock)base.Block;
}
