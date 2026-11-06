namespace Game.Blocks;

public class PressurePlateBlock : MountedElectricElementBlock
{
    public readonly BoundingBox[][] CollisionBoxesByData = CreateCollisionBoxes();

    public readonly string[] DisplayNamesByMaterial =
    [
        "木质压力板",
        "石质压力板"
    ];


    private static BoundingBox[][] CreateCollisionBoxes()
    {
        var boxes = new BoundingBox[16][];
        for (var i = 0; i < 2; i++)
        {
            for (var j = 0; j < 6; j++)
            {
                var num2 = SetMountingFace(SetMaterial(0, i), j);
                var matrix = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                var vector = Vector3.Transform(new Vector3(-0.5f, 0f, -0.5f), matrix);
                var vector2 = Vector3.Transform(new Vector3(0.5f, 0.0625f, 0.5f), matrix);
                vector.X = MathUtils.Round(vector.X * 100f) / 100f;
                vector.Y = MathUtils.Round(vector.Y * 100f) / 100f;
                vector.Z = MathUtils.Round(vector.Z * 100f) / 100f;
                vector2.X = MathUtils.Round(vector2.X * 100f) / 100f;
                vector2.Y = MathUtils.Round(vector2.Y * 100f) / 100f;
                vector2.Z = MathUtils.Round(vector2.Z * 100f) / 100f;
                boxes[num2] =
                [
                    new BoundingBox(
                        new Vector3(
                            MathUtils.Min(vector.X, vector2.X),
                            MathUtils.Min(vector.Y, vector2.Y),
                            MathUtils.Min(vector.Z, vector2.Z)
                        ),
                        new Vector3(
                            MathUtils.Max(vector.X, vector2.X),
                            MathUtils.Max(vector.Y, vector2.Y),
                            MathUtils.Max(vector.Z, vector2.Z)
                        )
                    )
                ];
            }
        }

        return boxes;
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 0);
        yield return Terrain.MakeBlockValue(BlockIndex, 0, 1);
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var data = SetMountingFace(Terrain.ExtractData(value), raycastResult.CellFace.Face);
        var value2 = Terrain.ReplaceData(value, data);
        BlockPlacementData result = default;
        result.Value = value2;
        result.CellFace = raycastResult.CellFace;
        return result;
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
        var material = GetMaterial(Terrain.ExtractData(oldValue));
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, 0, SetMaterial(0, material)),
                Count = 1
            }
        );
        showDebris = true;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num >= CollisionBoxesByData.Length ? [] : CollisionBoxesByData[num];
    }

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        return face != CellFace.OppositeFace(GetFace(value));
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new PressurePlateElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
    }

    public override ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var face2 = GetFace(value);
        if (face == face2 && SubsystemElectricity.GetConnectorDirection(face2, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }

    public static int GetMaterial(int data)
    {
        return data & 1;
    }

    public static int SetMaterial(int data, int material)
    {
        return (data & -2) | (material & 1);
    }

    public static int GetMountingFace(int data)
    {
        return (data >> 1) & 7;
    }

    public static int SetMountingFace(int data, int face)
    {
        return (data & -15) | ((face & 7) << 1);
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }
}
