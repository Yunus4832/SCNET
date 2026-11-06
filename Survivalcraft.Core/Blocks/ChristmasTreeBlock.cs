namespace Game.Blocks;

public class ChristmasTreeBlock : Block, IElectricElementBlock
{
    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new ChristmasTreeElectricElement(subsystemElectricity, new CellFace(x, y, z, 4), value);
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
        if (face == 4 && SubsystemElectricity.GetConnectorDirection(4, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }


    public override int GetEmittedLightAmount(int value)
    {
        if (!GetLightState(Terrain.ExtractData(value)))
        {
            return 0;
        }

        return EmittedLightAmount;
    }


    public static bool GetLightState(int data)
    {
        return (data & 1) != 0;
    }

    public static int SetLightState(int data, bool state)
    {
        if (!state)
        {
            return data & -2;
        }

        return data | 1;
    }
}
