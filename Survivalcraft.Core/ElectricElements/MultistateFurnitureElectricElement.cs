namespace Game.ElectricElements;

public class MultistateFurnitureElectricElement(
    SubsystemElectricity subsystemElectricity,
    Point3 point
) : FurnitureElectricElement(subsystemElectricity, point)
{
    private bool _isActionAllowed;

    private double? _lastActionTime;

    public override bool Simulate()
    {
        if (CalculateHighInputsCount() > 0)
        {
            if (!_isActionAllowed ||
                (_lastActionTime.HasValue &&
                 !(SubsystemElectricity.SubsystemTime.GameTime - _lastActionTime > 0.1)))
            {
                return false;
            }

            _isActionAllowed = false;
            _lastActionTime = SubsystemElectricity.SubsystemTime.GameTime;
            SubsystemElectricity.Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!
                .SwitchToNextState(CellFaces[0].X, CellFaces[0].Y, CellFaces[0].Z, false);
        }
        else
        {
            _isActionAllowed = true;
        }

        return false;
    }
}
