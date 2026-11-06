using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SmallGunpowderKegBlockPresentation(SmallGunpowderKegBlock block, BlockPresentationCatalog catalog)
    : GunpowderKegBlockPresentation(block, catalog, "Models/SmallGunpowderKeg")
{
    protected new SmallGunpowderKegBlock Block => (SmallGunpowderKegBlock)base.Block;
}
