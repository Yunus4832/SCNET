namespace Game.ElectricElements;

public class PistonElectricElement(
    SubsystemElectricity subsystemElectricity,
    Point3 point
) : ElectricElement(
    subsystemElectricity,
    new List<CellFace>
    {
        new(point.X, point.Y, point.Z, 0),
        new(point.X, point.Y, point.Z, 1),
        new(point.X, point.Y, point.Z, 2),
        new(point.X, point.Y, point.Z, 3),
        new(point.X, point.Y, point.Z, 4),
        new(point.X, point.Y, point.Z, 5)
    }
)
{
    private int _lastLength = -1;

    public override bool Simulate()
    {
        var inputVoltage = 0f;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                inputVoltage = MathUtils.Max(
                    inputVoltage,
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)
                );
            }
        }

        var extensionLength = MathUtils.Max((int)(inputVoltage * 15.999f) - 7, 0);
        if (extensionLength == _lastLength)
        {
            return false;
        }

        _lastLength = extensionLength;
        SubsystemElectricity.Project.FindSubsystem<SubsystemPistonBlockBehavior>(true)!
            .AdjustPiston(CellFaces[0].Point, extensionLength);

        return false;
    }
}
