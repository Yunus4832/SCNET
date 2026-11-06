using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class PostedSignBlock(string modelName, int attachedSignBlockIndex)
    : SignBlock, IElectricElementBlock, IPaintableBlock
{
    public int AttachedSignBlockIndex = attachedSignBlockIndex;


    public BoundingBox[][] CollisionBoxes = new BoundingBox[16][];


    public Vector3[] Directions = new Vector3[16];


    public Vector3[] SurfaceNormals = new Vector3[16];

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        return new SignElectricElement(subsystemElectricity, new CellFace(x, y, z, GetHanging(data) ? 5 : 4));
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
        if (GetHanging(Terrain.ExtractData(value)))
        {
            if (face != 5 || !SubsystemElectricity.GetConnectorDirection(face, 0, connectorFace).HasValue)
            {
                return null;
            }

            return ElectricConnectorType.Input;
        }

        if (face != 4 || !SubsystemElectricity.GetConnectorDirection(face, 0, connectorFace).HasValue)
        {
            return null;
        }

        return ElectricConnectorType.Input;
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
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var signNode = model.GetNode("Sign")!;
        var nodeAbsoluteTransform = signNode.AbsoluteTransform;
        var postNode = model.GetNode("Post")!;
        var nodeAbsoluteTransform2 = postNode.AbsoluteTransform;
        for (var i = 0; i < 16; i++)
        {
            var hanging = GetHanging(i);
            var m = Matrix.CreateRotationY(GetDirection(i) * (float)Math.PI / 4f) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            if (hanging)
            {
                m *= Matrix.CreateScale(1f, -1f, 1f) * Matrix.CreateTranslation(0f, 1f, 0f);
            }

            Directions[i] = m.Forward;
            var blockMesh = new CollisionGeometryBuilder();
            blockMesh.Append(signNode.Primitives[0], nodeAbsoluteTransform * m);
            var blockMesh2 = new CollisionGeometryBuilder();
            blockMesh2.Append(postNode.Primitives[0], nodeAbsoluteTransform2 * m);
            CollisionBoxes[i] = new BoundingBox[2];
            CollisionBoxes[i][0] = blockMesh.GetBounds();
            CollisionBoxes[i][1] = blockMesh2.GetBounds();
            SurfaceNormals[i] = -m.Forward;
        }

        base.Initialize();
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
        var color = GetColor(Terrain.ExtractData(oldValue));
        var data = SetColor(0, color);
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, 0, data),
                Count = 1
            }
        );
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var variant = GetVariant(Terrain.ExtractData(value));
        return CollisionBoxes[variant];
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var color = GetColor(Terrain.ExtractData(value));
        BlockPlacementData result;
        if (raycastResult.CellFace.Face < 4)
        {
            var data = AttachedSignBlock.SetFace(AttachedSignBlock.SetColor(0, color), raycastResult.CellFace.Face);
            result = default;
            result.Value = Terrain.MakeBlockValue(AttachedSignBlockIndex, 0, data);
            result.CellFace = raycastResult.CellFace;
            return result;
        }

        var forward = eyeForward;
        var num = float.MinValue;
        var direction = 0;
        for (var i = 0; i < 8; i++)
        {
            var num2 = Vector3.Dot(forward, Directions[i]);
            if (num2 > num)
            {
                num = num2;
                direction = i;
            }
        }

        var data2 = SetHanging(SetDirection(SetColor(0, color), direction), raycastResult.CellFace.Face == 5);
        result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, data2);
        result.CellFace = raycastResult.CellFace;
        return result;
    }


    public override Vector3 GetSignSurfaceNormal(int data)
    {
        return SurfaceNormals[GetVariant(data)];
    }

    public static int GetDirection(int data)
    {
        return data & 7;
    }

    public static int SetDirection(int data, int direction)
    {
        return (data & -8) | (direction & 7);
    }

    public static bool GetHanging(int data)
    {
        return (data & 8) != 0;
    }

    public static int SetHanging(int data, bool hanging)
    {
        if (!hanging)
        {
            return data & -9;
        }

        return data | 8;
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
