using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RottenBreadBlockPresentation(RottenBreadBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(block, catalog, "Models/Bread", Matrix.CreateTranslation(-0.375f, -0.25f, 0f), Color.White)
{
    protected new RottenBreadBlock Block => (RottenBreadBlock)base.Block;
}
