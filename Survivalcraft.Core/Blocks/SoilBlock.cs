namespace Game.Blocks;

public class SoilBlock : CubeBlock
{
    public const string TypeName = nameof(SoilBlock);

    public static readonly BoundingBox[] CollisionBoxes = [new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.9375f, 1f))];


    public static bool GetHydration(int data) => (data & 1) != 0;

    public static int GetNitrogen(int data) => (data >> 1) & 3;

    public static int SetHydration(int data, bool hydration)
    {
        if (!hydration)
        {
            return data & -2;
        }

        return data | 1;
    }

    public static int SetNitrogen(int data, int nitrogen)
    {
        nitrogen = MathUtils.Clamp(nitrogen, 0, 3);
        return (data & -7) | ((nitrogen & 3) << 1);
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetHydration(0, false));
        yield return Terrain.MakeBlockValue(BlockIndex, SetHydration(0, true));
        yield return Terrain.MakeBlockValue(BlockIndex, SetHydration(SetNitrogen(0, 3), true));
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => CollisionBoxes;

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value) => face != 5;

    public override bool IsCollapseSupportBlock(SubsystemTerrain subsystemTerrain, int value) => true;

    public override bool IsFaceNonAttachable(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int attachBlockValue
    )
    {
        var block = Catalog.Blocks[Terrain.ExtractContents(attachBlockValue)];
        return block is not BasePumpkinBlock &&
               base.IsFaceNonAttachable(subsystemTerrain, face, value, attachBlockValue);
    }
}
