using Game.Blocks.Collision;

namespace Game.Blocks;

public abstract class AttachedSignBlock(string modelName, int postedSignBlockIndex)
    : SignBlock, IElectricElementBlock, IPaintableBlock
{
    public readonly BoundingBox[][] CollisionBoxes = new BoundingBox[4][];


    public int PostedSignBlockIndex = postedSignBlockIndex;


    public readonly Vector3[] SurfaceNormals = new Vector3[4];

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        return new SignElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(data)));
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
        var data = Terrain.ExtractData(value);
        if (face != GetFace(data) ||
            !SubsystemElectricity.GetConnectorDirection(face, 0, connectorFace).HasValue)
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
        for (var i = 0; i < 4; i++)
        {
            var radians = (float)Math.PI / 2f * i;
            var m = Matrix.CreateTranslation(0f, 0f, -15f / 32f) *
                    Matrix.CreateRotationY(radians) *
                    Matrix.CreateTranslation(0.5f, -0.3125f, 0.5f);
            var blockMesh = new CollisionGeometryBuilder();
            blockMesh.Append(signNode.Primitives[0], nodeAbsoluteTransform * m);
            CollisionBoxes[i] = new BoundingBox[1];
            CollisionBoxes[i][0] = blockMesh.GetBounds();
            SurfaceNormals[i] = -m.Forward;
        }

        base.Initialize();
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
        var data = PostedSignBlock.SetColor(0, color);
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(PostedSignBlockIndex, data),
                Count = 1
            }
        );
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var face = GetFace(Terrain.ExtractData(value));
        return CollisionBoxes[face];
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        return default;
    }


    public override Vector3 GetSignSurfaceNormal(int data) => SurfaceNormals[GetFace(data)];

    public static int GetFace(int data) => data & 3;

    public static int SetFace(int data, int face) => (data & -4) | (face & 3);

    public static int? GetColor(int data)
    {
        if ((data & 4) != 0)
        {
            return (data >> 3) & 0xF;
        }

        return null;
    }

    public static int SetColor(int data, int? color)
    {
        if (color.HasValue)
        {
            return (data & -125) | 4 | ((color.Value & 0xF) << 3);
        }

        return data & -125;
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = true;
        var data = Terrain.ExtractData(value);
        return pistonFace == GetFace(data);
    }
}
