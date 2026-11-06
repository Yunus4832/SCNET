using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RottenMeatBlockPresentation(RottenMeatBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(block, catalog, "Models/Meat", Matrix.CreateTranslation(-0.0625f, 0f, 0f), Color.White)
{
    protected new RottenMeatBlock Block => (RottenMeatBlock)base.Block;
}
