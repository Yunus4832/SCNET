using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CookedFishBlockPresentation(CookedFishBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(block, catalog, "Models/Fish", Matrix.Identity, new Color(160, 80, 40))
{
    protected new CookedFishBlock Block => (CookedFishBlock)base.Block;
}
