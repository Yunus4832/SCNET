namespace Game.ElectricElements;

public class FourLedElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    private Color _color;

    private readonly GlowPoint[] _glowPoints = new GlowPoint[4];

    private readonly SubsystemGlow _subsystemGlow = subsystemElectricity.Project.FindSubsystem<SubsystemGlow>(true)!;

    private float _voltage;

    public override void OnAdded()
    {
        var cellFace = CellFaces[0];
        var data = Terrain.ExtractData(
            SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z));
        var mountingFace = FourLedBlock.GetMountingFace(data);
        _color = LedBlock.LedColors[FourLedBlock.GetColor(data)];
        for (var i = 0; i < 4; i++)
        {
            var num = i % 2 == 0 ? 1 : -1;
            var num2 = i / 2 == 0 ? 1 : -1;
            var v = new Vector3(cellFace.X + 0.5f, cellFace.Y + 0.5f, cellFace.Z + 0.5f);
            var vector = CellFace.FaceToVector3(mountingFace);
            var vector2 = mountingFace < 4 ? Vector3.UnitY : Vector3.UnitX;
            var vector3 = Vector3.Cross(vector, vector2);
            _glowPoints[i] = _subsystemGlow.AddGlowPoint();
            _glowPoints[i].Position = v - 0.4375f * CellFace.FaceToVector3(mountingFace) + 0.25f * vector3 * num +
                                      0.25f * vector2 * num2;
            _glowPoints[i].Forward = vector;
            _glowPoints[i].Up = vector2;
            _glowPoints[i].Right = vector3;
            _glowPoints[i].Color = Color.Transparent;
            _glowPoints[i].Size = 0.26f;
            _glowPoints[i].FarSize = 0.26f;
            _glowPoints[i].FarDistance = 1f;
            _glowPoints[i].Type = GlowPointType.Square;
        }
    }

    public override void OnRemoved()
    {
        for (var i = 0; i < 4; i++)
        {
            _subsystemGlow.RemoveGlowPoint(_glowPoints[i]);
        }
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
        for (var i = 0; i < 4; i++)
        {
            _glowPoints[i].Color = (num & (1 << i)) != 0 ? _color : Color.Transparent;
        }

        return false;
    }
}
