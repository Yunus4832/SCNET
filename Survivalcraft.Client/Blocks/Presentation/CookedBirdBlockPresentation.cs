using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CookedBirdBlockPresentation(CookedBirdBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(block, catalog, "Models/Bird", Matrix.Identity, new Color(150, 69, 15))
{
    protected new CookedBirdBlock Block => (CookedBirdBlock)base.Block;
}
