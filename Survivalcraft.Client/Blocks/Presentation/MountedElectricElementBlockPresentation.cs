using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class MountedElectricElementBlockPresentation(
    MountedElectricElementBlock block,
    BlockPresentationCatalog catalog
) : BlockPresentation(block, catalog)
{
    protected new MountedElectricElementBlock Block => (MountedElectricElementBlock)base.Block;
}
