namespace Game.ElectricElements;

public class PressurePlateElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    private int _lastPressFrameIndex;

    private float _pressure;

    private float _voltage;

    public void Press(float pressure)
    {
        _lastPressFrameIndex = Time.FrameIndex;
        if (!(pressure > _pressure))
        {
            return;
        }

        _pressure = pressure;
        var cellFace = CellFaces[0];
        SubsystemElectricity.SubsystemAudio.PlaySound("Audio/BlockPlaced", 1f, 0.3f,
            new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 2.5f, true);
        SubsystemElectricity.QueueElectricElementForSimulation(this, SubsystemElectricity.CircuitStep + 1);
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        if (_pressure > 0f && Time.FrameIndex - _lastPressFrameIndex < 2)
        {
            _voltage = PressureToVoltage(_pressure);
            SubsystemElectricity.QueueElectricElementForSimulation(this, SubsystemElectricity.CircuitStep + 10);
        }
        else
        {
            if (IsSignalHigh(_voltage))
            {
                var cellFace = CellFaces[0];
                SubsystemElectricity.SubsystemAudio.PlaySound("Audio/BlockPlaced", 0.6f, -0.1f,
                    new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 2.5f, true);
            }

            _voltage = 0f;
            _pressure = 0f;
        }

        return _voltage.UncloseTo(voltage);
    }

    public override void OnCollide(CellFace cellFace, float velocity, ComponentBody componentBody)
    {
        Press(componentBody.Mass);
        componentBody.ApplyImpulse(new Vector3(0f, -2E-05f, 0f));
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        var num = Terrain.ExtractContents(worldItem.Value);
        var block = BlocksManager.Blocks[num];
        Press(1f * block.Density);
    }

    public static float PressureToVoltage(float pressure)
    {
        return pressure switch
        {
            <= 0f => 0f,
            < 1f => 8f / 15f,
            < 2f => 0.6f,
            < 5f => 2f / 3f,
            < 25f => 11f / 15f,
            < 100f => 0.8f,
            < 250f => 13f / 15f,
            < 500f => 14f / 15f,
            _ => 1f
        };
    }
}
