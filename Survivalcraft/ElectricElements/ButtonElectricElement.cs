namespace Game.ElectricElements;

public class ButtonElectricElement : MountedElectricElement
{
    private float _pressedVoltage;

    private float _voltage;

    private bool _wasPressed;

    public ButtonElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace,
        int value
    ) : base(subsystemElectricity, cellFace)
    {
        var voltageLevel = ButtonBlock.GetVoltageLevel(Terrain.ExtractData(value));
        _pressedVoltage = voltageLevel / 15f;
    }

    public void Press()
    {
        if (_wasPressed || _voltage != 0f)
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
            _voltage = _pressedVoltage;
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
