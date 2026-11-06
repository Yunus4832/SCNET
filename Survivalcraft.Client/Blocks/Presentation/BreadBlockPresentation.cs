using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BreadBlockPresentation(BreadBlock block, BlockPresentationCatalog catalog) : FoodBlockPresentation(
    block,
    catalog,
    "Models/Bread",
    Matrix.Identity,
    Color.White
)
{
    protected new BreadBlock Block => (BreadBlock)base.Block;
}
