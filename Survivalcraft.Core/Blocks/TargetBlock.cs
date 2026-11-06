namespace Game.Blocks;

public class TargetBlock : MountedElectricElementBlock
{
    public BoundingBox[][] BoundingBoxes = new BoundingBox[][]
    {
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.0625f))],
        [new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(0.0625f, 1f, 1f))],
        [new BoundingBox(new Vector3(0f, 0f, 0.9375f), new Vector3(1f, 1f, 1f))],
        [new BoundingBox(new Vector3(0.9375f, 0f, 0f), new Vector3(1f, 1f, 1f))]
    };

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var mountingFace = GetMountingFace(Terrain.ExtractData(value));
        return mountingFace is >= 0 and < 4
            ? BoundingBoxes[mountingFace]
            : base.GetCustomCollisionBoxes(terrain, value);
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        BlockPlacementData result = default;
        if (raycastResult.CellFace.Face >= 4)
        {
            return result;
        }

        result.CellFace = raycastResult.CellFace;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetMountingFace(0, raycastResult.CellFace.Face));

        return result;
    }


    public static int GetMountingFace(int data)
    {
        return data & 3;
    }

    public static int SetMountingFace(int data, int face)
    {
        return (data & -4) | (face & 3);
    }

    public override int GetFace(int value)
    {
        return GetMountingFace(Terrain.ExtractData(value));
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new TargetElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
