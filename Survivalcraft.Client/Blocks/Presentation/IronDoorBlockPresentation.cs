using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class IronDoorBlockPresentation(IronDoorBlock block, BlockPresentationCatalog catalog)
    : DoorBlockPresentation(block, catalog, "Models/IronDoor")
{
    protected new IronDoorBlock Block => (IronDoorBlock)base.Block;
}
