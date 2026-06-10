using Engine.Graphics;

namespace Game.Blocks;

public class SeedsBlock : FlatBlock
{
    public enum SeedType
    {
        TallGrass,
        RedFlower,
        PurpleFlower,
        WhiteFlower,
        WildRye,
        Rye,
        Cotton,
        Pumpkin
    }

    public const int Index = 173;

    public override IEnumerable<int> GetCreativeValues()
    {
        return EnumUtils.GetEnumValues(typeof(SeedType))
            .Select(enumValue => Terrain.MakeBlockValue(173, 0, enumValue))
            .ToList();
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        return Terrain.ExtractData(value) switch
        {
            0 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 1)),
            1 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 2)),
            2 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 3)),
            3 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 4)),
            4 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 5)),
            5 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 6)),
            6 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 7)),
            7 => LanguageManager.Get(LanguageManager.Get("BaseSeedBlock", 8)),
            _ => string.Empty
        };
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        var num = Terrain.ExtractData(value);
        return num is 5 or 4 ? 74 : 75;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.CellFace = raycastResult.CellFace;
        if (raycastResult.CellFace.Face == 4)
        {
            result.Value = Terrain.ExtractData(value) switch
            {
                0 => Terrain.MakeBlockValue(19, 0, TallGrassBlock.SetIsSmall(0, true)),
                1 => Terrain.MakeBlockValue(20, 0, FlowerBlock.SetIsSmall(0, true)),
                2 => Terrain.MakeBlockValue(24, 0, FlowerBlock.SetIsSmall(0, true)),
                3 => Terrain.MakeBlockValue(25, 0, FlowerBlock.SetIsSmall(0, true)),
                4 => Terrain.MakeBlockValue(174, 0, RyeBlock.SetSize(RyeBlock.SetIsWild(0, false), 0)),
                5 => Terrain.MakeBlockValue(174, 0, RyeBlock.SetSize(RyeBlock.SetIsWild(0, false), 0)),
                6 => Terrain.MakeBlockValue(204, 0, CottonBlock.SetSize(CottonBlock.SetIsWild(0, false), 0)),
                7 => Terrain.MakeBlockValue(131, 0, BasePumpkinBlock.SetSize(BasePumpkinBlock.SetIsDead(0, false), 0)),
                _ => result.Value
            };
        }

        return result;
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

        BlocksManager.DrawFlatOrImageExtrusionBlock(
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
