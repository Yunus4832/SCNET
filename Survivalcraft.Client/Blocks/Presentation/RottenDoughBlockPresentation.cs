using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RottenDoughBlockPresentation(RottenDoughBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(
        block,
        catalog,
        "Models/Bread",
        Matrix.CreateTranslation(-0.375f, -0.25f, 0f),
        new Color(192, 255, 212)
    )
{
    protected new RottenDoughBlock Block => (RottenDoughBlock)base.Block;
}
