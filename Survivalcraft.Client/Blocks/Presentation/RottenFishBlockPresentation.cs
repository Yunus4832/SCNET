using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RottenFishBlockPresentation(RottenFishBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(block, catalog, "Models/Fish", Matrix.CreateTranslation(-0.125f, 0.125f, 0f), Color.White)
{
    protected new RottenFishBlock Block => (RottenFishBlock)base.Block;
}
