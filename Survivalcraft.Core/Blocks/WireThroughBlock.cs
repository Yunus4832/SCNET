namespace Game.Blocks;

public abstract class WireThroughBlock : CubeBlock, IElectricWireElementBlock, IElectricElementBlock
{
    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        throw new InvalidOperationException("WireThroughBlock not support CreateElectricElement");
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
        var wiredFace = GetWiredFace(Terrain.ExtractData(value));
        if ((face == wiredFace || face == CellFace.OppositeFace(wiredFace)) &&
            connectorFace == CellFace.OppositeFace(face))
        {
            return ElectricConnectorType.InputOutput;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public int GetConnectedWireFacesMask(int value, int face)
    {
        var wiredFace = GetWiredFace(Terrain.ExtractData(value));
        if (wiredFace == face || CellFace.OppositeFace(wiredFace) == face)
        {
            return (1 << wiredFace) | (1 << CellFace.OppositeFace(wiredFace));
        }

        return 0;
    }


    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var forward = eyeForward;
        var num = float.NegativeInfinity;
        var wiredFace = 0;
        for (var i = 0; i < 6; i++)
        {
            var num2 = Vector3.Dot(CellFace.FaceToVector3(i), forward);
            if (!(num2 > num))
            {
                continue;
            }

            num = num2;
            wiredFace = i;
        }

        BlockPlacementData result = default;
        result.Value = Terrain.MakeBlockValue(BlockIndex, 0, SetWiredFace(0, wiredFace));
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public static int GetWiredFace(int data)
    {
        return (data & 3) switch
        {
            0 => 0,
            1 => 1,
            _ => 4
        };
    }

    public static int SetWiredFace(int data, int wiredFace)
    {
        data &= -4;
        return wiredFace switch
        {
            0 or 2 => data,
            1 or 3 => data | 1,
            _ => data | 2
        };
    }
}
