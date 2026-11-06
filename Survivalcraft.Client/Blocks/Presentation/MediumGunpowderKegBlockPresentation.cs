using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MediumGunpowderKegBlockPresentation(MediumGunpowderKegBlock block, BlockPresentationCatalog catalog)
    : GunpowderKegBlockPresentation(block, catalog, "Models/MediumGunpowderKeg")
{
    protected new MediumGunpowderKegBlock Block => (MediumGunpowderKegBlock)base.Block;
}
