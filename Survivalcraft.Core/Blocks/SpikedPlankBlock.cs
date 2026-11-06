using Game.Blocks.Collision;

namespace Game.Blocks;

public class SpikedPlankBlock : MountedElectricElementBlock
{
    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[12][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/SpikedPlanks.glb");

    public override void Initialize()
    {
        var blockPositionsByData = new CollisionGeometryBuilder[12];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var array = new[]
        {
            "SpikedPlankRetracted",
            "SpikedPlank"
        };
        for (var i = 0; i < 2; i++)
        {
            var name = array[i];
            var modelNode = model.GetNode(name)!;
            var nodeAbsoluteTransform = modelNode.AbsoluteTransform;
            for (var j = 0; j < 6; j++)
            {
                var num = SetMountingFace(SetSpikesState(0, i != 0), j);
                var m = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                blockPositionsByData[num] = new CollisionGeometryBuilder();
                blockPositionsByData[num].Append(modelNode.Primitives[0], nodeAbsoluteTransform * m);
                CollisionBoxesByData[num] = [blockPositionsByData[num].GetBounds()];
            }
        }
    }


    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return face != CellFace.OppositeFace(mountingFace);
    }

    public override bool ShouldAvoid(int value)
    {
        return GetSpikesState(Terrain.ExtractData(value));
    }

    public static bool GetSpikesState(int data)
    {
        return (data & 1) == 0;
    }

    public static int SetSpikesState(int data, bool spikesState)
    {
        if (spikesState)
        {
            return data & -2;
        }

        return data | 1;
    }

    public static int GetMountingFace(int data)
    {
        return ((data >> 1) + 4) % 6;
    }

    public static int SetMountingFace(int data, int face)
    {
        data &= -15;
        data |= (((face + 2) % 6) & 7) << 1;
        return data;
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var data = SetMountingFace(SetSpikesState(Terrain.ExtractData(value), true), raycastResult.CellFace.Face);
        BlockPlacementData result = default;
        result.Value = Terrain.ReplaceData(value, data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        if (num >= CollisionBoxesByData.Length)
        {
            return base.GetCustomCollisionBoxes(terrain, value);
        }

        return CollisionBoxesByData[num];
    }


    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new SpikedPlankElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
            return ElectricConnectorType.Input;
        }

        return null;
    }
}
