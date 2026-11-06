using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class LargeIncendiaryKegBlockPresentation(LargeIncendiaryKegBlock block, BlockPresentationCatalog catalog)
    : GunpowderKegBlockPresentation(block, catalog, "Models/LargeGunpowderKeg")
{
    protected new LargeIncendiaryKegBlock Block => (LargeIncendiaryKegBlock)base.Block;
}
