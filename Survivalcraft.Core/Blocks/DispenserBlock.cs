namespace Game.Blocks;

public class DispenserBlock : CubeBlock, IElectricElementBlock
{
    public enum Mode
    {
        Dispense = 0,

        Shoot = 1
    }

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new DispenserElectricElement(subsystemElectricity, new Point3(x, y, z));
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
        return ElectricConnectorType.Input;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = eyeForward;
        var positiveZAlignment = Vector3.Dot(forward, Vector3.UnitZ);
        var positiveXAlignment = Vector3.Dot(forward, Vector3.UnitX);
        var negativeZAlignment = Vector3.Dot(forward, -Vector3.UnitZ);
        var negativeXAlignment = Vector3.Dot(forward, -Vector3.UnitX);
        var positiveYAlignment = Vector3.Dot(forward, Vector3.UnitY);
        var negativeYAlignment = Vector3.Dot(forward, -Vector3.UnitY);
        var minimumAlignment = MathUtils.Min(
            MathUtils.Min(positiveZAlignment, positiveXAlignment, negativeZAlignment),
            MathUtils.Min(negativeXAlignment, positiveYAlignment, negativeYAlignment)
        );
        var direction = 0;
        if (positiveZAlignment.CloseTo(minimumAlignment))
        {
            direction = 0;
        }
        else if (positiveXAlignment.CloseTo(minimumAlignment))
        {
            direction = 1;
        }
        else if (negativeZAlignment.CloseTo(minimumAlignment))
        {
            direction = 2;
        }
        else if (negativeXAlignment.CloseTo(minimumAlignment))
        {
            direction = 3;
        }
        else if (positiveYAlignment.CloseTo(minimumAlignment))
        {
            direction = 4;
        }
        else if (negativeYAlignment.CloseTo(minimumAlignment))
        {
            direction = 5;
        }

        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, SetDirection(0, direction));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public static int GetDirection(int data)
    {
        return data & 7;
    }

    public static int SetDirection(int data, int direction)
    {
        return (data & -8) | (direction & 7);
    }

    public static Mode GetMode(int data)
    {
        return (data & 8) != 0 ? Mode.Shoot : Mode.Dispense;
    }

    public static int SetMode(int data, Mode mode)
    {
        return (data & -9) | (mode != 0 ? 8 : 0);
    }

    public static bool GetAcceptsDrops(int data)
    {
        return (data & 0x10) != 0;
    }

    public static int SetAcceptsDrops(int data, bool acceptsDrops)
    {
        return (data & -17) | (acceptsDrops ? 16 : 0);
    }
}
