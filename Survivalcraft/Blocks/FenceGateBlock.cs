using Engine.Graphics;

namespace Game.Blocks;

public abstract class FenceGateBlock : Block, IElectricElementBlock, IPaintableBlock
{
    public BlockMesh[] BlockMeshes = new BlockMesh[16];

    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];

    public BlockMesh[] ColoredBlockMeshes = new BlockMesh[16];

    public int ColoredTextureSlot;

    public bool DoubleSided;

    public string ModelName;

    public float PivotDistance;

    public Color PostColor;

    public BlockMesh StandaloneBlockMesh = new();

    public BlockMesh StandaloneColoredBlockMesh = new();

    public Color UnpaintedColor;

    public bool UseAlphaTest;

    public FenceGateBlock(string modelName, float pivotDistance, bool doubleSided, bool useAlphaTest,
        int coloredTextureSlot, Color postColor, Color unpaintedColor)
    {
        ModelName = modelName;
        PivotDistance = pivotDistance;
        DoubleSided = doubleSided;
        UseAlphaTest = useAlphaTest;
        ColoredTextureSlot = coloredTextureSlot;
        PostColor = postColor;
        UnpaintedColor = unpaintedColor;
    }

    public ElectricElement CreateElectricElement(SubsystemElectricity subsystemElectricity, int value, int x, int y,
        int z)
    {
        var data = Terrain.ExtractData(value);
        return new FenceGateElectricElement(subsystemElectricity, new CellFace(x, y, z, GetHingeFace(data)));
    }

    public ElectricConnectorType? GetConnectorType(SubsystemTerrain terrain, int value, int face, int connectorFace,
        int x, int y, int z)
    {
        var hingeFace = GetHingeFace(Terrain.ExtractData(value));
        if (face == hingeFace)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        var data = Terrain.ExtractData(value);
        return Terrain.ReplaceData(value, SetColor(data, color));
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var postMesh = model.FindMesh("Post")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            postMesh.ParentBone
            ?? throw new InvalidOperationException("Required PostMesh.ParentBone is null")
        );
        var planksMesh = model.FindMesh("Planks")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            planksMesh.ParentBone
            ?? throw new InvalidOperationException("Required PlanksMesh.ParentBone is null")
        );
        for (var i = 0; i < 16; i++)
        {
            var rotation = GetRotation(i);
            var open = GetOpen(i);
            var rightHanded = GetRightHanded(i);
            float num = !rightHanded ? 1 : -1;
            var identity = Matrix.Identity;
            identity *= Matrix.CreateScale(0f - num, 1f, 1f);
            identity *= Matrix.CreateTranslation((0.5f - PivotDistance) * num, 0f, 0f) *
                        Matrix.CreateRotationY(open ? num * (float)Math.PI / 2f : 0f) *
                        Matrix.CreateTranslation((0f - (0.5f - PivotDistance)) * num, 0f, 0f);
            identity *= Matrix.CreateTranslation(0f, 0f, 0f) * Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) *
                        Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            BlockMeshes[i] = new BlockMesh();
            BlockMeshes[i].AppendModelMeshPart(postMesh.MeshParts[0], boneAbsoluteTransform * identity,
                false, !rightHanded, false, false, PostColor);
            BlockMeshes[i].AppendModelMeshPart(planksMesh.MeshParts[0],
                boneAbsoluteTransform2 * identity, false, !rightHanded, false, false, Color.White);
            if (DoubleSided)
            {
                BlockMeshes[i].AppendModelMeshPart(planksMesh.MeshParts[0],
                    boneAbsoluteTransform2 * identity, false, rightHanded, false, true, Color.White);
            }

            ColoredBlockMeshes[i] = new BlockMesh();
            ColoredBlockMeshes[i].AppendBlockMesh(BlockMeshes[i]);
            BlockMeshes[i].TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
                TextureSlot / 16 / 16f, 0f));
            ColoredBlockMeshes[i].TransformTextureCoordinates(
                Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
            var boundingBox = BlockMeshes[i].CalculateBoundingBox();
            var min = boundingBox.Min;
            var max = boundingBox.Max;

            // 调用 MathUtils.Saturate 对 Min 和 Max 的各分量进行修改
            min.X = MathUtils.Saturate(min.X);
            min.Y = MathUtils.Saturate(min.Y);
            min.Z = MathUtils.Saturate(min.Z);

            max.X = MathUtils.Saturate(max.X);
            max.Y = MathUtils.Saturate(max.Y);
            max.Z = MathUtils.Saturate(max.Z);

            // 将修改后的值赋回 BoundingBox
            boundingBox.Min = min;
            boundingBox.Max = max;

            // 设置碰撞箱
            CollisionBoxes[i] = new BoundingBox[1]
            {
                boundingBox
            };
        }

        StandaloneBlockMesh.AppendModelMeshPart(postMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, PostColor);
        StandaloneBlockMesh.AppendModelMeshPart(planksMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        if (DoubleSided)
        {
            StandaloneBlockMesh.AppendModelMeshPart(planksMesh.MeshParts[0],
                boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), false, true, false, true,
                Color.White);
        }

        StandaloneColoredBlockMesh.AppendBlockMesh(StandaloneBlockMesh);
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(TextureSlot % 16 / 16f,
            TextureSlot / 16 / 16f, 0f));
        StandaloneColoredBlockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(ColoredTextureSlot % 16 / 16f, ColoredTextureSlot / 16 / 16f, 0f));
        base.Initialize();
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        return SubsystemPalette.GetName(color, base.GetDisplayName(subsystemTerrain, value));
    }

    public override string GetCategory(int value)
    {
        return !GetColor(Terrain.ExtractData(value)).HasValue ? base.GetCategory(value) : "Painted";
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
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
        showDebris = true;
        var data = SetVariant(Terrain.ExtractData(oldValue), 0);
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(BlockIndex, 0, data),
            Count = 1
        });
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation)
            .Forward;
        var num = Vector3.Dot(forward, Vector3.UnitZ);
        var num2 = Vector3.Dot(forward, Vector3.UnitX);
        var num3 = Vector3.Dot(forward, -Vector3.UnitZ);
        var num4 = Vector3.Dot(forward, -Vector3.UnitX);
        var num5 = 0;
        if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            num5 = 2;
        }
        else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            num5 = 3;
        }
        else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            num5 = 0;
        }
        else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            num5 = 1;
        }

        var point = CellFace.FaceToPoint3(raycastResult.CellFace.Face);
        var num6 = raycastResult.CellFace.X + point.X;
        var y = raycastResult.CellFace.Y + point.Y;
        var num7 = raycastResult.CellFace.Z + point.Z;
        var num8 = 0;
        var num9 = 0;
        switch (num5)
        {
            case 0:
                num8 = -1;
                break;
            case 1:
                num9 = 1;
                break;
            case 2:
                num8 = 1;
                break;
            default:
                num9 = -1;
                break;
        }

        var cellValue = subsystemTerrain.Terrain.GetCellValue(num6 + num8, y, num7 + num9);
        var cellValue2 = subsystemTerrain.Terrain.GetCellValue(num6 - num8, y, num7 - num9);
        var block = BlocksManager.Blocks[Terrain.ExtractContents(cellValue)];
        var block2 = BlocksManager.Blocks[Terrain.ExtractContents(cellValue2)];
        var data = Terrain.ExtractData(cellValue);
        var data2 = Terrain.ExtractData(cellValue2);
        var data3 = SetRightHanded(
            rightHanded: (block is FenceGateBlock && GetRotation(data) == num5) ||
                         ((block2 is not FenceGateBlock || GetRotation(data2) != num5) && !block.Collidable),
            data: SetOpen(SetRotation(Terrain.ExtractData(value), num5), false));
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), data3);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var color = GetColor(Terrain.ExtractData(value));
        if (color.HasValue)
        {
            return new BlockDebrisParticleSystem(
                subsystemTerrain,
                position,
                strength,
                DestructionDebrisScale,
                SubsystemPalette.GetColor(subsystemTerrain, color),
                ColoredTextureSlot
            );
        }

        return new BlockDebrisParticleSystem(
            subsystemTerrain,
            position,
            strength,
            DestructionDebrisScale,
            Color.White,
            TextureSlot
        );
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
        var variant = GetVariant(data);
        var color = GetColor(data);
        if (color.HasValue)
        {
            generator.GenerateMeshVertices(this, x, y, z, ColoredBlockMeshes[variant],
                SubsystemPalette.GetColor(generator, color), null,
                UseAlphaTest ? geometry.SubsetAlphaTest : geometry.SubsetOpaque);
        }
        else
        {
            generator.GenerateMeshVertices(this, x, y, z, BlockMeshes[variant], UnpaintedColor, null,
                UseAlphaTest ? geometry.SubsetAlphaTest : geometry.SubsetOpaque);
        }

        generator.GenerateWireVertices(value, x, y, z, GetHingeFace(data), PivotDistance * 2f, Vector2.Zero,
            geometry.SubsetOpaque);
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
        var color2 = GetColor(Terrain.ExtractData(value));
        if (color2.HasValue)
        {
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                StandaloneColoredBlockMesh,
                color * SubsystemPalette.GetColor(environmentData, color2),
                size,
                ref matrix,
                environmentData
            );
            return;
        }

        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color * UnpaintedColor,
            size,
            ref matrix,
            environmentData
        );
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var variant = GetVariant(Terrain.ExtractData(value));
        return CollisionBoxes[variant];
    }

    public static int GetRotation(int data)
    {
        return data & 3;
    }

    public static bool GetOpen(int data)
    {
        return (data & 4) != 0;
    }

    public static bool GetRightHanded(int data)
    {
        return (data & 8) == 0;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -4) | (rotation & 3);
    }

    public static int SetOpen(int data, bool open)
    {
        if (!open)
        {
            return data & -5;
        }

        return data | 4;
    }

    public static int SetRightHanded(int data, bool rightHanded)
    {
        if (rightHanded)
        {
            return data & -9;
        }

        return data | 8;
    }

    public static int GetHingeFace(int data)
    {
        var rotation = GetRotation(data);
        var num = rotation - 1 < 0 ? 3 : rotation - 1;
        if (!GetRightHanded(data))
        {
            num = CellFace.OppositeFace(num);
        }

        return num;
    }

    public static int GetVariant(int data)
    {
        return data & 0xF;
    }

    public static int SetVariant(int data, int variant)
    {
        return (data & -16) | (variant & 0xF);
    }

    public static int? GetColor(int data)
    {
        if ((data & 0x10) != 0)
        {
            return (data >> 5) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -497) | 0x10 | ((color.Value & 0xF) << 5);
        }

        return data & -497;
    }
}
