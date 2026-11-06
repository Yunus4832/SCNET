using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RawBirdBlockPresentation(RawBirdBlock block, BlockPresentationCatalog catalog) : FoodBlockPresentation(
    block,
    catalog,
    "Models/Bird",
    Matrix.Identity,
    new Color(224, 170, 164)
)
{
    protected new RawBirdBlock Block => (RawBirdBlock)base.Block;
}
