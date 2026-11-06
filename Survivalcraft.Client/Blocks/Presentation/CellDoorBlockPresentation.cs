using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CellDoorBlockPresentation(CellDoorBlock block, BlockPresentationCatalog catalog)
    : DoorBlockPresentation(block, catalog, "Models/CellDoor")
{
    protected new CellDoorBlock Block => (CellDoorBlock)base.Block;
}
