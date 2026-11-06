using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RottenBirdBlockPresentation(RottenBirdBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(block, catalog, "Models/Bird", Matrix.CreateTranslation(-0.9375f, 0.4375f, 0f), Color.White)
{
    protected new RottenBirdBlock Block => (RottenBirdBlock)base.Block;
}
