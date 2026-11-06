using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class LargeGunpowderKegBlockPresentation(LargeGunpowderKegBlock block, BlockPresentationCatalog catalog)
    : GunpowderKegBlockPresentation(block, catalog, "Models/LargeGunpowderKeg")
{
    protected new LargeGunpowderKegBlock Block => (LargeGunpowderKegBlock)base.Block;
}
