using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CookedMeatBlockPresentation(CookedMeatBlock block, BlockPresentationCatalog catalog)
    : FoodBlockPresentation(block, catalog, "Models/Meat", Matrix.Identity, new Color(155, 122, 51))
{
    protected new CookedMeatBlock Block => (CookedMeatBlock)base.Block;
}
