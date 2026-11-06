using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SeedsBlockPresentation(SeedsBlock block, BlockPresentationCatalog catalog)
    : FlatBlockPresentation(block, catalog)
{
    protected new SeedsBlock Block => (SeedsBlock)base.Block;

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        return Terrain.ExtractData(value) switch
        {
            0 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 1)),
            1 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 2)),
            2 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 3)),
            3 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 4)),
            4 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 5)),
            5 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 6)),
            6 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 7)),
            7 => Localization.ClientLanguage.Catalog.Get(Localization.ClientLanguage.Catalog.Get("BaseSeedBlock", 8)),
            _ => string.Empty
        };
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        var num = Terrain.ExtractData(value);
        return num is 5 or 4 ? 74 : 75;
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        switch (Terrain.ExtractData(value))
        {
            case 0:
                color *= new Color(160, 150, 125);
                break;
            case 1:
                color *= new Color(192, 160, 160);
                break;
            case 2:
                color *= new Color(192, 160, 192);
                break;
            case 3:
                color *= new Color(192, 192, 192);
                break;
            case 4:
                color *= new Color(60, 138, 76);
                break;
            case 6:
                color *= new Color(255, 255, 255);
                break;
            case 7:
                color *= new Color(240, 225, 190);
                break;
        }

        Catalog.DrawFlatOrImageExtrusionBlock(
            primitivesRenderer,
            value,
            size,
            ref matrix,
            null,
            color,
            false,
            environmentData
        );
    }
}
