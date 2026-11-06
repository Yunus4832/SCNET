using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SaplingBlockPresentation(SaplingBlock block, BlockPresentationCatalog catalog)
    : CrossBlockPresentation(block, catalog)
{
    protected new SaplingBlock Block => (SaplingBlock)base.Block;

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        return Terrain.ExtractData(value) switch
        {
            0 => Localization.ClientLanguage.Catalog.Get("BaseSaplingBlock", 1),
            1 => Localization.ClientLanguage.Catalog.Get("BaseSaplingBlock", 2),
            2 => Localization.ClientLanguage.Catalog.Get("BaseSaplingBlock", 3),
            3 => Localization.ClientLanguage.Catalog.Get("BaseSaplingBlock", 4),
            4 => Localization.ClientLanguage.Catalog.Get("BaseSaplingBlock", 5),
            _ => Localization.ClientLanguage.Catalog.Get("BaseSaplingBlock", 6)
        };
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        return Terrain.ExtractData(value) switch
        {
            0 => 56,
            1 => 72,
            2 => 73,
            3 => 73,
            4 => 72,
            5 => 110,
            _ => 56
        };
    }
}
