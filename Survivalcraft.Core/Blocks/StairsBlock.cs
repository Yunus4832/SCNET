namespace Game.Blocks;

public abstract class StairsBlock : Block, IPaintableBlock
{
    public enum CornerType
    {
        None = 0,

        OneQuarter = 1,

        ThreeQuarters = 2
    }

    public BoundingBox[][] CollisionBoxes = StairsCollisionShapes.Create();


    public virtual int? GetPaintColor(int value)
    {
        return GetColor(Terrain.ExtractData(value));
    }

    public virtual int Paint(SubsystemTerrain? terrain, int value, int? color)
    {
        return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(Terrain.ExtractData(value), color));
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

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var data = Terrain.ExtractData(value);
        var isUpsideDown = GetIsUpsideDown(data);
        switch (face)
        {
            case 4:
                return !isUpsideDown;
            case 5:
                return isUpsideDown;
            default:
                switch (GetCornerType(data))
                {
                    case CornerType.None:
                    {
                        var rotation2 = GetRotation(data);
                        return face != ((rotation2 + 2) & 3);
                    }
                    case CornerType.OneQuarter:
                        return true;
                    default:
                    {
                        var rotation = GetRotation(data);
                        if (face != ((rotation + 1) & 3))
                        {
                            return face != ((rotation + 2) & 3);
                        }

                        return false;
                    }
                }
        }
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
        var rotation = 0;
        if (num.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 2;
        }
        else if (num2.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 3;
        }
        else if (num3.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 0;
        }
        else if (num4.CloseTo(MathUtils.Max(num, num2, num3, num4)))
        {
            rotation = 1;
        }

        var isUpsideDown = raycastResult.CellFace.Face == 5;
        var data = Terrain.ExtractData(value);
        BlockPlacementData result = default;
        result.Value =
            Terrain.MakeBlockValue(BlockIndex, 0, SetIsUpsideDown(SetRotation(data, rotation), isUpsideDown));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var data = Terrain.ExtractData(value);
        return CollisionBoxes[GetVariant(data)];
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
        var data = Terrain.ExtractData(oldValue);
        var data2 = SetColor(0, GetColor(data));
        var value = Terrain.MakeBlockValue(BlockIndex, 0, data2);
        dropValues.Add(
            new BlockDropValue
            {
                Value = value,
                Count = 1
            }
        );
    }


    public static Point3 RotationToDirection(int rotation)
    {
        return CellFace.FaceToPoint3((rotation + 2) % 4);
    }

    public static int GetRotation(int data)
    {
        return data & 3;
    }

    public static int SetRotation(int data, int rotation)
    {
        return (data & -4) | (rotation & 3);
    }

    public static bool GetIsUpsideDown(int data)
    {
        return (data & 4) != 0;
    }

    public static int SetIsUpsideDown(int data, bool isUpsideDown)
    {
        if (isUpsideDown)
        {
            return data | 4;
        }

        return data & -5;
    }

    public static CornerType GetCornerType(int data)
    {
        return (CornerType)((data >> 3) & 3);
    }

    public static int SetCornerType(int data, CornerType cornerType)
    {
        return (data & -25) | ((int)(cornerType & (CornerType)3) << 3);
    }

    public static int? GetColor(int data)
    {
        if ((data & 0x20) != 0)
        {
            return (data >> 6) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -993) | 0x20 | ((color.Value & 0xF) << 6);
        }

        return data & -993;
    }

    public static int GetVariant(int data)
    {
        return data & 0x1F;
    }
}
