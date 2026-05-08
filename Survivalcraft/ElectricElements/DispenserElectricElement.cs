namespace Game.ElectricElements;

public class DispenserElectricElement : ElectricElement
{
    private bool _isDispenseAllowed = true;

    private double? _lastDispenseTime;

    private readonly SubsystemBlockEntities _subsystemBlockEntities;

    public DispenserElectricElement(SubsystemElectricity subsystemElectricity, Point3 point)
        : base(subsystemElectricity, new List<CellFace>
        {
            new(point.X, point.Y, point.Z, 0),
            new(point.X, point.Y, point.Z, 1),
            new(point.X, point.Y, point.Z, 2),
            new(point.X, point.Y, point.Z, 3),
            new(point.X, point.Y, point.Z, 4),
            new(point.X, point.Y, point.Z, 5)
        })
    {
        _subsystemBlockEntities = SubsystemElectricity.Project.FindSubsystem<SubsystemBlockEntities>(true)!;
    }

    public override bool Simulate()
    {
        if (CalculateHighInputsCount() > 0)
        {
            if (!_isDispenseAllowed ||
                (_lastDispenseTime.HasValue &&
                 !(SubsystemElectricity.SubsystemTime.GameTime - _lastDispenseTime > 0.1)))
            {
                return false;
            }

            _isDispenseAllowed = false;
            _lastDispenseTime = SubsystemElectricity.SubsystemTime.GameTime;
            _subsystemBlockEntities
                .GetBlockEntity(CellFaces[0].Point.X, CellFaces[0].Point.Y, CellFaces[0].Point.Z)?.Entity
                .FindComponent<ComponentDispenser>()?.Dispense();
        }
        else
        {
            _isDispenseAllowed = true;
        }

        return false;
    }
}
