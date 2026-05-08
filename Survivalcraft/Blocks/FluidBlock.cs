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

        for (var i = 0; i < BlocksManager.Blocks.Length; i++)
        {
            var block = BlocksManager.Blocks[i];
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

    public void GenerateFluidTerrainVertices(
        BlockGeometryGenerator generator,
        int value,
        int x,
        int y,
        int z,
        Color sideColor,
        Color topColor,
        TerrainGeometrySubset[] subset
    )
    {
        var data = Terrain.ExtractData(value);
        if (GetIsTop(data))
        {
            var terrain = generator.Terrain;
            var cellValueFast = terrain.GetCellValueFast(x - 1, y, z - 1);
            var cellValueFast2 = terrain.GetCellValueFast(x, y, z - 1);
            var cellValueFast3 = terrain.GetCellValueFast(x + 1, y, z - 1);
            var cellValueFast4 = terrain.GetCellValueFast(x - 1, y, z);
            var cellValueFast5 = terrain.GetCellValueFast(x + 1, y, z);
            var cellValueFast6 = terrain.GetCellValueFast(x - 1, y, z + 1);
            var cellValueFast7 = terrain.GetCellValueFast(x, y, z + 1);
            var cellValueFast8 = terrain.GetCellValueFast(x + 1, y, z + 1);
            var h = CalculateNeighborHeight(cellValueFast);
            var num = CalculateNeighborHeight(cellValueFast2);
            var h2 = CalculateNeighborHeight(cellValueFast3);
            var num2 = CalculateNeighborHeight(cellValueFast4);
            var num3 = CalculateNeighborHeight(cellValueFast5);
            var h3 = CalculateNeighborHeight(cellValueFast6);
            var num4 = CalculateNeighborHeight(cellValueFast7);
            var h4 = CalculateNeighborHeight(cellValueFast8);
            var levelHeight = GetLevelHeight(GetLevel(data));
            var height = CalculateFluidVertexHeight(h, num, num2, levelHeight);
            var height2 = CalculateFluidVertexHeight(num, h2, levelHeight, num3);
            var height3 = CalculateFluidVertexHeight(levelHeight, num3, num4, h4);
            var height4 = CalculateFluidVertexHeight(num2, levelHeight, h3, num4);
            var x2 = ZeroSubst(num3, levelHeight) - ZeroSubst(num2, levelHeight);
            var x3 = ZeroSubst(num4, levelHeight) - ZeroSubst(num, levelHeight);
            var overrideTopTextureSlot = TextureSlot - (int)MathUtils.Sign(x2) - 16 * (int)MathUtils.Sign(x3);
            generator.GenerateCubeVertices(this, value, x, y, z, height, height2, height3, height4, sideColor, topColor,
                topColor, topColor, topColor, overrideTopTextureSlot, subset);
        }
        else
        {
            generator.GenerateCubeVertices(this, value, x, y, z, sideColor, subset);
        }
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
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), 0);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        if (face >= 4)
        {
            return TextureSlot;
        }

        return TextureSlot + 16;
    }

    public override bool ShouldGenerateFace(
        SubsystemTerrain subsystemTerrain,
        int face,
        int value,
        int neighborValue,
        int x,
        int y,
        int z
    )
    {
        var contents = Terrain.ExtractContents(neighborValue);
        if (IsTheSameFluid(contents))
        {
            return false;
        }

        return base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue, x, y, z);
    }

    public override bool ShouldGenerateFace(SubsystemTerrain subsystemTerrain, int face, int value, int neighborValue)
    {
        var contents = Terrain.ExtractContents(neighborValue);
        if (IsTheSameFluid(contents))
        {
            return false;
        }

        return base.ShouldGenerateFace(subsystemTerrain, face, value, neighborValue);
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
