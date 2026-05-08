namespace Game.ElectricElements;

public class OneLedElectricElement(
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
        var cellFace = CellFaces[0];
        var data = Terrain.ExtractData(
            SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z));
        var mountingFace = FourLedBlock.GetMountingFace(data);
        _color = LedBlock.LedColors[FourLedBlock.GetColor(data)];
        var v = new Vector3(cellFace.X + 0.5f, cellFace.Y + 0.5f, cellFace.Z + 0.5f);
        var vector = CellFace.FaceToVector3(mountingFace);
        var vector2 = mountingFace < 4 ? Vector3.UnitY : Vector3.UnitX;
        var right = Vector3.Cross(vector, vector2);
        _glowPoint = _subsystemGlow.AddGlowPoint();
        _glowPoint.Position = v - 0.4375f * CellFace.FaceToVector3(mountingFace);
        _glowPoint.Forward = vector;
        _glowPoint.Up = vector2;
        _glowPoint.Right = right;
        _glowPoint.Color = Color.Transparent;
        _glowPoint.Size = 0.52f;
        _glowPoint.FarSize = 0.52f;
        _glowPoint.FarDistance = 1f;
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
