using Engine.Graphics;

namespace Game.Blocks;

public abstract class BasePumpkinBlock(bool isRotten) : Block
{
    public BlockMesh[] BlockMeshesBySize = new BlockMesh[8];

    public BoundingBox[][] CollisionBoxesBySize = new BoundingBox[8][];

    public bool IsRotten = isRotten;

    public BlockMesh[] StandaloneBlockMeshesBySize = new BlockMesh[8];

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Pumpkins");
        var pumpkinMesh = model.FindMesh("Pumpkin")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            pumpkinMesh.ParentBone ??
            throw new InvalidOperationException("Required PumpkinMesh.ParentBone is null")
        );
        for (var i = 0; i < 8; i++)
        {
            var num = MathUtils.Lerp(0.2f, 1f, i / 7f);
            var num2 = MathUtils.Min(0.3f * num, 0.7f * (1f - num));
            Color color;
            if (IsRotten)
            {
                color = Color.White;
            }
            else
            {
                color = Color.Lerp(new Color(0, 128, 128), new Color(80, 255, 255), i / 7f);
                if (i == 7)
                {
                    color.R = byte.MaxValue;
                }
            }

            BlockMeshesBySize[i] = new BlockMesh();
            if (i >= 1)
            {
                BlockMeshesBySize[i].AppendModelMeshPart(pumpkinMesh.MeshParts[0],
                    boneAbsoluteTransform * Matrix.CreateScale(num) *
                    Matrix.CreateTranslation(0.5f + num2, 0f, 0.5f + num2), false, false, false, false, color);
            }

            if (IsRotten)
            {
                BlockMeshesBySize[i].TransformTextureCoordinates(Matrix.CreateTranslation(-0.375f, 0.25f, 0f));
            }

            StandaloneBlockMeshesBySize[i] = new BlockMesh();
            StandaloneBlockMeshesBySize[i].AppendModelMeshPart(pumpkinMesh.MeshParts[0],
                boneAbsoluteTransform * Matrix.CreateScale(num) * Matrix.CreateTranslation(0f, -0.23f, 0f), false,
                false, false, false, color);
            if (IsRotten)
            {
                StandaloneBlockMeshesBySize[i]
                    .TransformTextureCoordinates(Matrix.CreateTranslation(-0.375f, 0.25f, 0f));
            }
        }

        for (var j = 0; j < 8; j++)
        {
            var boundingBox = BlockMeshesBySize[j].Vertices.Count > 0
                ? BlockMeshesBySize[j].CalculateBoundingBox()
                : new BoundingBox(new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f));
            var num3 = boundingBox.Max.X - boundingBox.Min.X;
            if (num3 < 0.8f)
            {
                var num4 = (0.8f - num3) / 2f;
                // 获取 Min 和 Max 的值
                var min = boundingBox.Min;
                var max = boundingBox.Max;

                // 修改 Min 和 Max 的分量
                min.X -= num4;
                min.Z -= num4;

                max.X += num4;
                max.Y = 0.4f;
                max.Z += num4;

                // 将修改后的值赋回
                boundingBox.Min = min;
                boundingBox.Max = max;
            }

            CollisionBoxesBySize[j] = [boundingBox];
        }

        base.Initialize();
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var size = GetSize(Terrain.ExtractData(value));
        return CollisionBoxesBySize[size];
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        var size = GetSize(data);
        var isDead = GetIsDead(data);
        if (size >= 1)
        {
            generator.GenerateMeshVertices(this, x, y, z, BlockMeshesBySize[size], Color.White, null,
                geometry.SubsetOpaque);
        }

        if (size == 0)
        {
            generator.GenerateCrossingFaceVertices(this, value, x, y, z, new Color(160, 160, 160), 11,
                geometry.SubsetAlphaTest);
        }
        else if (size < 7 && !isDead)
        {
            generator.GenerateCrossingFaceVertices(this, value, x, y, z, Color.White, 28, geometry.SubsetAlphaTest);
        }
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color, float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        var size2 = GetSize(Terrain.ExtractData(value));
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMeshesBySize[size2],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override int GetShadowStrength(int value)
    {
        return GetSize(Terrain.ExtractData(value));
    }

    public override float GetNutritionalValue(int value)
    {
        if (GetSize(Terrain.ExtractData(value)) != 7)
        {
            return 0f;
        }

        return base.GetNutritionalValue(value);
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        var size = GetSize(Terrain.ExtractData(oldValue));
        if (size >= 1)
        {
            var value = SetDamage(Terrain.MakeBlockValue(BlockIndex, 0, SetSize(SetIsDead(0, true), size)),
                GetDamage(oldValue));
            dropValues.Add(new BlockDropValue
            {
                Value = value,
                Count = 1
            });
        }

        showDebris = true;
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var size = GetSize(Terrain.ExtractData(value));
        var num = MathUtils.Lerp(0.2f, 1f, size / 7f);
        var color = size == 7 ? Color.White : new Color(0, 128, 128);
        return new BlockDebrisParticleSystem(subsystemTerrain, position, 1.5f * strength, DestructionDebrisScale * num,
            color, TextureSlot);
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var size = GetSize(Terrain.ExtractData(value));
        if (IsRotten)
        {
            return size >= 7 ? "腐烂的南瓜" : "腐烂未成熟的南瓜";
        }

        return size >= 7 ? "南瓜" : "未成熟的南瓜";
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSize(SetIsDead(0, true), 1));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSize(SetIsDead(0, true), 3));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSize(SetIsDead(0, true), 5));
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetSize(SetIsDead(0, true), 7));
    }

    public static int GetSize(int data)
    {
        return 7 - (data & 7);
    }

    public static int SetSize(int data, int size)
    {
        return (data & -8) | (7 - (size & 7));
    }

    public static bool GetIsDead(int data)
    {
        return (data & 8) != 0;
    }

    public static int SetIsDead(int data, bool isDead)
    {
        if (!isDead)
        {
            return data & -9;
        }

        return data | 8;
    }

    public override int GetDamage(int value)
    {
        return (Terrain.ExtractData(value) & 0x10) >> 4;
    }

    public override int SetDamage(int value, int damage)
    {
        var num = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, (num & -17) | ((damage & 1) << 4));
    }

    public override int GetDamageDestructionValue(int value)
    {
        if (IsRotten)
        {
            return 0;
        }

        var data = Terrain.ExtractData(value);
        return SetDamage(Terrain.MakeBlockValue(244, 0, data), 0);
    }

    public override int GetRotPeriod(int value)
    {
        return !GetIsDead(Terrain.ExtractData(value)) ? 0 : RotPeriod;
    }
}
