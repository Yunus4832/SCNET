using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class DoughBlockPresentation(DoughBlock block, BlockPresentationCatalog catalog) : FoodBlockPresentation(
    block,
    catalog,
    "Models/Bread",
    Matrix.CreateTranslation(0.5625f, -0.875f, 0f),
    new Color(241, 231, 214)
)
{
    protected new DoughBlock Block => (DoughBlock)base.Block;
}
