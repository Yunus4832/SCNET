using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class WoodenDoorBlockPresentation(WoodenDoorBlock block, BlockPresentationCatalog catalog)
    : DoorBlockPresentation(block, catalog, "Models/WoodenDoor")
{
    protected new WoodenDoorBlock Block => (WoodenDoorBlock)base.Block;
}
