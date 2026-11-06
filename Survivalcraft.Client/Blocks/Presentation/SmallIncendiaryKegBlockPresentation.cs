using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SmallIncendiaryKegBlockPresentation(SmallIncendiaryKegBlock block, BlockPresentationCatalog catalog)
    : GunpowderKegBlockPresentation(block, catalog, "Models/SmallGunpowderKeg")
{
    protected new SmallIncendiaryKegBlock Block => (SmallIncendiaryKegBlock)base.Block;
}
