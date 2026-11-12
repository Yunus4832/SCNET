using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class FenceGateBlock(string modelName, float pivotDistance)
    : Block, IElectricElementBlock, IPaintableBlock
{
    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];


    public float PivotDistance = pivotDistance;


    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        return new FenceGateElectricElement(subsystemElectricity, new CellFace(x, y, z, GetHingeFace(data)));
    }

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
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

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:" + modelName + ".glb");

    public override void Initialize()
    {
        var blockPositions = new CollisionGeometryBuilder[16];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var postNode = model.GetNode("Post")!;
        var nodeAbsoluteTransform = postNode.AbsoluteTransform;
        var planksNode = model.GetNode("Planks")!;
        var nodeAbsoluteTransform2 = planksNode.AbsoluteTransform;
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
            identity *= Matrix.CreateTranslation(0f, 0f, 0f) *
                        Matrix.CreateRotationY(rotation * (float)Math.PI / 2f) *
                        Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            blockPositions[i] = new CollisionGeometryBuilder();
            blockPositions[i].Append(postNode.Primitives[0], nodeAbsoluteTransform * identity);
            blockPositions[i].Append(planksNode.Primitives[0], nodeAbsoluteTransform2 * identity);

            var boundingBox = blockPositions[i].GetBounds();
            var min = boundingBox.Min;
            var max = boundingBox.Max;
            min.X = MathUtils.Saturate(min.X);
            min.Y = MathUtils.Saturate(min.Y);
            min.Z = MathUtils.Saturate(min.Z);
            max.X = MathUtils.Saturate(max.X);
            max.Y = MathUtils.Saturate(max.Y);
            max.Z = MathUtils.Saturate(max.Z);
            boundingBox.Min = min;
            boundingBox.Max = max;
            CollisionBoxes[i] =
            [
                boundingBox
            ];
        }

        base.Initialize();
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, SetColor(0, null));
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, SetColor(0, i));
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
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, data),
                Count = 1
            }
        );
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = eyeForward;
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
        var block = Catalog.Blocks[Terrain.ExtractContents(cellValue)];
        var block2 = Catalog.Blocks[Terrain.ExtractContents(cellValue2)];
        var data = Terrain.ExtractData(cellValue);
        var data2 = Terrain.ExtractData(cellValue2);
        var data3 = SetRightHanded(
            rightHanded: (block is FenceGateBlock && GetRotation(data) == num5) ||
                         ((block2 is not FenceGateBlock || GetRotation(data2) != num5) && !block.Collidable),
            data: SetOpen(SetRotation(Terrain.ExtractData(value), num5), false)
        );
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, BlockIndex), data3);
        result.CellFace = raycastResult.CellFace;
        return result;
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
