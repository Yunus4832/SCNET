using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class MediumIncendiaryKegBlockPresentation(MediumIncendiaryKegBlock block, BlockPresentationCatalog catalog)
    : GunpowderKegBlockPresentation(block, catalog, "Models/MediumGunpowderKeg")
{
    protected new MediumIncendiaryKegBlock Block => (MediumIncendiaryKegBlock)base.Block;
}
