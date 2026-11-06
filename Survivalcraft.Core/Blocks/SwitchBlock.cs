using Game.Blocks.Collision;

namespace Game.Blocks;

public class SwitchBlock : MountedElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByIndex = new BoundingBox[12][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Switch.glb");

    public override void Initialize()
    {
        var blockPositionsByIndex = new CollisionGeometryBuilder[12];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var bodyNode = model.GetNode("Body")!;
        var nodeAbsoluteTransform = bodyNode.AbsoluteTransform;
        var leverNode = model.GetNode("Lever")!;
        var nodeAbsoluteTransform2 = leverNode.AbsoluteTransform;
        for (var i = 0; i < 6; i++)
        {
            for (var j = 0; j < 2; j++)
            {
                var num = (i << 1) | j;
                var matrix = i >= 4
                    ? i != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                var matrix2 = Matrix.CreateRotationX(j == 0 ? MathUtils.DegToRad(30f) : MathUtils.DegToRad(-30f));
                blockPositionsByIndex[num] = new CollisionGeometryBuilder();
                blockPositionsByIndex[num].Append(bodyNode.Primitives[0], nodeAbsoluteTransform * matrix);
                blockPositionsByIndex[num].Append(leverNode.Primitives[0], nodeAbsoluteTransform2 * matrix2 * matrix);
                CollisionBoxesByIndex[num] = [blockPositionsByIndex[num].GetBounds()];
            }
        }
    }


    public static bool GetLeverState(int value)
    {
        return (Terrain.ExtractData(value) & 1) != 0;
    }

    public static int SetLeverState(int value, bool state)
    {
        return Terrain.ReplaceData(value, state ? Terrain.ExtractData(value) | 1 : Terrain.ExtractData(value) & -2);
    }

    public static int GetVoltageLevel(int data)
    {
        return 15 - ((data >> 4) & 0xF);
    }

    public static int SetVoltageLevel(int data, int voltageLevel)
    {
        return (data & -241) | ((15 - (voltageLevel & 0xF)) << 4);
    }

    public override int GetFace(int value)
    {
        return (Terrain.ExtractData(value) >> 1) & 7;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var result = default(BlockPlacementData);
        result.Value = Terrain.ReplaceData(value, raycastResult.CellFace.Face << 1);
        var data = SetVoltageLevel(Terrain.ExtractData(result.Value), GetVoltageLevel(Terrain.ExtractData(value)));
        result.Value = Terrain.ReplaceData(value, data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = CalculateIndex(value);
        return num >= CollisionBoxesByIndex.Length ? [] : CollisionBoxesByIndex[num];
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new SwitchElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)), value);
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

    public int CalculateIndex(int value)
    {
        var face = GetFace(value);
        var leverState = GetLeverState(value);
        return (face << 1) | (leverState ? 1 : 0);
    }
}
