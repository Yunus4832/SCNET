using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RawMeatBlockPresentation(RawMeatBlock block, BlockPresentationCatalog catalog) : FoodBlockPresentation(
    block,
    catalog,
    "Models/Meat",
    Matrix.Identity,
    Color.White
)
{
    protected new RawMeatBlock Block => (RawMeatBlock)base.Block;
}
