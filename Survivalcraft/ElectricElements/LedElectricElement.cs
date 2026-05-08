namespace Game.ElectricElements;

public class LedElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    private Color _color;

    private GlowPoint _glowPoint = null!;

    private readonly SubsystemGlow _subsystemGlow = subsystemElectricity.Project.FindSubsystem<SubsystemGlow>(true)!;

    private float _voltage;

    public override void OnAdded()
    {
        _glowPoint = _subsystemGlow.AddGlowPoint();
        var cellFace = CellFaces[0];
        var data = Terrain.ExtractData(
            SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z));
        var mountingFace = LedBlock.GetMountingFace(data);
        _color = LedBlock.LedColors[LedBlock.GetColor(data)];
        var v = new Vector3(cellFace.X + 0.5f, cellFace.Y + 0.5f, cellFace.Z + 0.5f);
        _glowPoint.Position = v - 0.4375f * CellFace.FaceToVector3(mountingFace);
        _glowPoint.Forward = CellFace.FaceToVector3(mountingFace);
        _glowPoint.Up = mountingFace < 4 ? Vector3.UnitY : Vector3.UnitX;
        _glowPoint.Right = Vector3.Cross(_glowPoint.Forward, _glowPoint.Up);
        _glowPoint.Color = Color.Transparent;
        _glowPoint.Size = 0.0324f;
        _glowPoint.FarSize = 0.0324f;
        _glowPoint.FarDistance = 0f;
        _glowPoint.Type = GlowPointType.Square;
    }

    public override void OnRemoved()
    {
        _subsystemGlow.RemoveGlowPoint(_glowPoint);
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        _voltage = CalculateVoltage();
        if (IsSignalHigh(_voltage) != IsSignalHigh(voltage))
        {
            _glowPoint.Color = IsSignalHigh(_voltage) ? _color : Color.Transparent;
        }

        return false;
    }

    public float CalculateVoltage()
    {
        return CalculateHighInputsCount() > 0 ? 1 : 0;
    }
}
