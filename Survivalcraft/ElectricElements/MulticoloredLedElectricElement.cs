namespace Game.ElectricElements;

public class MulticoloredLedElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    private GlowPoint _glowPoint = null!;

    private readonly SubsystemGlow _subsystemGlow = subsystemElectricity.Project.FindSubsystem<SubsystemGlow>(true)!;

    private float _voltage;

    public override void OnAdded()
    {
        _glowPoint = _subsystemGlow.AddGlowPoint();
        var cellFace = CellFaces[0];
        var mountingFace = MulticoloredLedBlock.GetMountingFace(Terrain.ExtractData(
            SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z)));
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
        _voltage = 0f;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                _voltage = MathUtils.Max(_voltage,
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
            }
        }

        if (_voltage.CloseTo(voltage))
        {
            return false;
        }

        var num = (int)MathUtils.Round(_voltage * 15f);
        _glowPoint.Color = num >= 8 ? LedBlock.LedColors[MathUtils.Clamp(num - 8, 0, 7)] : Color.Transparent;

        return false;
    }
}
