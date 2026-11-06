using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RottenEggBlockPresentation(RottenEggBlock block, BlockPresentationCatalog catalog) : FoodBlockPresentation(
    block,
    catalog,
    "Models/RottenEgg",
    Matrix.Identity,
    Color.White
)
{
    protected new RottenEggBlock Block => (RottenEggBlock)base.Block;
}
