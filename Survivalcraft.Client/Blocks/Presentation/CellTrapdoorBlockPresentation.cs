using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CellTrapdoorBlockPresentation(CellTrapdoorBlock block, BlockPresentationCatalog catalog)
    : TrapdoorBlockPresentation(block, catalog, "Models/CellTrapdoor")
{
    protected new CellTrapdoorBlock Block => (CellTrapdoorBlock)base.Block;
}
