namespace Game.Blocks;

public class SoundGeneratorBlock() : RotatableMountedElectricElementBlock("Models/Gates", "SoundGenerator", 0.5f)
{
    public const int Index = 183;

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new SoundGeneratorElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
        var data = Terrain.ExtractData(value);
        if (GetFace(value) != face)
        {
            return null;
        }

        var connectorDirection =
            SubsystemElectricity.GetConnectorDirection(GetFace(value), GetRotation(data), connectorFace);
        if (connectorDirection is
            ElectricConnectorDirection.Bottom or
            ElectricConnectorDirection.Top or
            ElectricConnectorDirection.Right or
            ElectricConnectorDirection.Left or
            ElectricConnectorDirection.In
           )
        {
            return ElectricConnectorType.Input;
        }

        return null;
    }
}
