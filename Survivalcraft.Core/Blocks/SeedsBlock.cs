namespace Game.Blocks;

public class SeedsBlock : FlatBlock
{
    public enum SeedType
    {
        TallGrass = 0,

        RedFlower = 1,

        PurpleFlower = 2,

        WhiteFlower = 3,

        WildRye = 4,

        Rye = 5,

        Cotton = 6,

        Pumpkin = 7
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        return EnumUtils.GetEnumValues(typeof(SeedType))
            .Select(enumValue => Terrain.MakeBlockValue(BlockIndex, enumValue))
            .ToList();
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
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
                0 => Terrain.MakeBlockValue(19, TallGrassBlock.SetIsSmall(0, true)),
                1 => Terrain.MakeBlockValue(20, FlowerBlock.SetIsSmall(0, true)),
                2 => Terrain.MakeBlockValue(24, FlowerBlock.SetIsSmall(0, true)),
                3 => Terrain.MakeBlockValue(25, FlowerBlock.SetIsSmall(0, true)),
                4 => Terrain.MakeBlockValue(174, RyeBlock.SetSize(RyeBlock.SetIsWild(0, false), 0)),
                5 => Terrain.MakeBlockValue(174, RyeBlock.SetSize(RyeBlock.SetIsWild(0, false), 0)),
                6 => Terrain.MakeBlockValue(204, CottonBlock.SetSize(CottonBlock.SetIsWild(0, false), 0)),
                7 => Terrain.MakeBlockValue(131, BasePumpkinBlock.SetSize(BasePumpkinBlock.SetIsDead(0, false), 0)),
                _ => result.Value
            };
        }

        return result;
    }
}
