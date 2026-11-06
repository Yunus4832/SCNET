using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RawFishBlockPresentation(RawFishBlock block, BlockPresentationCatalog catalog) : FoodBlockPresentation(
    block,
    catalog,
    "Models/Fish",
    Matrix.Identity,
    Color.White
)
{
    protected new RawFishBlock Block => (RawFishBlock)base.Block;
}
