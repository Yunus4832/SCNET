namespace Game.ElectricElements;

public class ButtonFurnitureElectricElement(
    SubsystemElectricity subsystemElectricity,
    Point3 point
) : FurnitureElectricElement(subsystemElectricity, point)
{
    private float _voltage;

    private bool _wasPressed;

    public void Press()
    {
        if (_wasPressed || IsSignalHigh(_voltage))
        {
            return;
        }

        _wasPressed = true;
        var cellFace = CellFaces[0];
        SubsystemElectricity.SubsystemAudio.PlaySound("Audio/Click", 1f, 0f,
            new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 2f, true);
        SubsystemElectricity.QueueElectricElementForSimulation(this, SubsystemElectricity.CircuitStep + 1);
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        if (_wasPressed)
        {
            _wasPressed = false;
            _voltage = 1f;
            SubsystemElectricity.QueueElectricElementForSimulation(this, SubsystemElectricity.CircuitStep + 10);
        }
        else
        {
            _voltage = 0f;
        }

        return _voltage.UncloseTo(voltage);
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        Press();
        return true;
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        Press();
    }
}
