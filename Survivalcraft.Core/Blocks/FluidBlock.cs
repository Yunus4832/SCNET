namespace Game.Blocks;

public abstract class FluidBlock : CubeBlock
{
    public BoundingBox[][] BoundingBoxesByLevel = new BoundingBox[16][];

    public float[] HeightByLevel = new float[16];

    public bool[] TheSameFluidsByIndex = new bool[1024];

    public int MaxLevel;

    public FluidBlock(int maxLevel)
    {
        MaxLevel = maxLevel;
        for (var i = 0; i < BoundingBoxesByLevel.Length; i++)
        {
            var num = 0.875f * MathUtils.Saturate(1f - i / (float)MaxLevel);
            HeightByLevel[i] = num;
            BoundingBoxesByLevel[i] =
            [
                new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, num, 1f))
            ];
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        TypeInfo? typeInfo = null;
        var typeInfo2 = GetType().GetTypeInfo();
        while (typeInfo2 is not null)
        {
            if (typeInfo2.BaseType == typeof(FluidBlock))
            {
                typeInfo = typeInfo2;
                break;
            }

            typeInfo2 = typeInfo2.BaseType?.GetTypeInfo();
        }

        if (typeInfo is null)
        {
            throw new InvalidOperationException("Fluid type not found.");
        }

        for (var i = 0; i < Catalog.Blocks.Length; i++)
        {
            var block = Catalog.Blocks[i];
            TheSameFluidsByIndex[i] = block.GetType().GetTypeInfo() == typeInfo ||
                                      block.GetType().GetTypeInfo().IsSubclassOf(typeInfo.AsType());
        }
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return BoundingBoxesByLevel[GetLevel(Terrain.ExtractData(value))];
    }

    public bool IsTheSameFluid(int contents)
    {
        return TheSameFluidsByIndex[contents];
    }

    public float GetLevelHeight(int level)
    {
        return HeightByLevel[level];
    }


    public static float ZeroSubst(float v, float subst)
    {
        return v != 0f ? v : subst;
    }

    public static float CalculateFluidVertexHeight(float h1, float h2, float h3, float h4)
    {
        var num = MathUtils.Max(h1, h2, h3, h4);
        if (!(num < 1f))
        {
            return 1f;
        }

        if (h1.CloseTo(0.01f) || h2.CloseTo(0.01f) || h3.CloseTo(0.01f) || h4.CloseTo(0.01f))
        {
            return 0f;
        }

        return num;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 0);
        result.CellFace = raycastResult.CellFace;
        return result;
    }


    public float CalculateNeighborHeight(int value)
    {
        var num = Terrain.ExtractContents(value);
        if (!IsTheSameFluid(num))
        {
            return num == 0 ? 0.01f : 0f;
        }

        var data = Terrain.ExtractData(value);
        return GetIsTop(data) ? GetLevelHeight(GetLevel(data)) : 1f;
    }

    public override bool IsHeatBlocker(int value) => true;

    public static int GetLevel(int data) => data & 0xF;

    public static int SetLevel(int data, int level) => (data & -16) | (level & 0xF);

    public static bool GetIsTop(int data) => (data & 0x10) != 0;

    public static int SetIsTop(int data, bool isTop)
    {
        if (!isTop)
        {
            return data & -17;
        }

        return data | 0x10;
    }

    public override bool IsCollapseDestructibleBlock(int value) => false;
}
