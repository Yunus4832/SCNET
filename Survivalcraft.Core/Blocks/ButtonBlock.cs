using Game.Blocks.Collision;

namespace Game.Blocks;

public class ButtonBlock : MountedElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByFace = new BoundingBox[6][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Button.glb");

    public override void Initialize()
    {
        var blockPositionsByFace = new CollisionGeometryBuilder[6];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var buttonNode = model.GetNode("Button")!;
        var nodeAbsoluteTransform = buttonNode.AbsoluteTransform;
        for (var i = 0; i < 6; i++)
        {
            var matrix = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            blockPositionsByFace[i] = new CollisionGeometryBuilder();
            blockPositionsByFace[i].Append(buttonNode.Primitives[0], nodeAbsoluteTransform * matrix);
            CollisionBoxesByFace[i] = [blockPositionsByFace[i].GetBounds()];
        }
    }


    public static int GetVoltageLevel(int data)
    {
        return 15 - ((data >> 3) & 0xF);
    }

    public static int SetVoltageLevel(int data, int voltageLevel)
    {
        return (data & -121) | ((15 - (voltageLevel & 0xF)) << 3);
    }

    public override int GetFace(int value)
    {
        return Terrain.ExtractData(value) & 7;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var result = default(BlockPlacementData);
        result.Value = Terrain.ReplaceData(value, raycastResult.CellFace.Face);
        var data = SetVoltageLevel(Terrain.ExtractData(result.Value), GetVoltageLevel(Terrain.ExtractData(value)));
        result.Value = Terrain.ReplaceData(value, data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var face = GetFace(value);
        return face >= CollisionBoxesByFace.Length ? [] : CollisionBoxesByFace[face];
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new ButtonElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)), value);
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
}
