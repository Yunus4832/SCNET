namespace Game.ElectricElements;

public class SevenSegmentDisplayElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    private readonly Vector2[] _centers =
    [
        new(0f, 6f),
        new(-4f, 3f),
        new(-4f, -3f),
        new(0f, -6f),
        new(4f, -3f),
        new(4f, 3f),
        new(0f, 0f)
    ];

    private Color _color;

    private readonly GlowPoint[] _glowPoints = new GlowPoint[7];

    private readonly int[] _patterns =
    [
        63,
        6,
        91,
        79,
        102,
        109,
        125,
        7,
        127,
        111,
        119,
        124,
        57,
        94,
        121,
        113
    ];

    private readonly Vector2[] _sizes =
    [
        new(3.2f, 1f),
        new(1f, 2.3f),
        new(1f, 2.3f),
        new(3.2f, 1f),
        new(1f, 2.3f),
        new(1f, 2.3f),
        new(3.2f, 1f)
    ];

    private readonly SubsystemGlow _subsystemGlow = subsystemElectricity.Project.FindSubsystem<SubsystemGlow>(true)!;

    private float _voltage = 1f / 0f;

    public override void OnAdded()
    {
        var cellFace = CellFaces[0];
        var data = Terrain.ExtractData(
            SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z));
        var mountingFace = SevenSegmentDisplayBlock.GetMountingFace(data);
        _color = LedBlock.LedColors[SevenSegmentDisplayBlock.GetColor(data)];
        for (var i = 0; i < 7; i++)
        {
            var v = new Vector3(cellFace.X + 0.5f, cellFace.Y + 0.5f, cellFace.Z + 0.5f);
            var vector = CellFace.FaceToVector3(mountingFace);
            var vector2 = mountingFace < 4 ? Vector3.UnitY : Vector3.UnitX;
            var v2 = Vector3.Cross(vector, vector2);
            _glowPoints[i] = _subsystemGlow.AddGlowPoint();
            _glowPoints[i].Position = v - 0.4375f * CellFace.FaceToVector3(mountingFace) +
                                      _centers[i].X * 0.0625f * v2 + _centers[i].Y * 0.0625f * vector2;
            _glowPoints[i].Forward = vector;
            _glowPoints[i].Right = v2 * _sizes[i].X * 0.0625f;
            _glowPoints[i].Up = vector2 * _sizes[i].Y * 0.0625f;
            _glowPoints[i].Color = Color.Transparent;
            _glowPoints[i].Size = 1.35f;
            _glowPoints[i].FarSize = 1.35f;
            _glowPoints[i].FarDistance = 1f;
            _glowPoints[i].Type = _sizes[i].X > _sizes[i].Y
                ? GlowPointType.HorizontalRectangle
                : GlowPointType.VerticalRectangle;
        }
    }

    public override void OnRemoved()
    {
        for (var i = 0; i < 7; i++)
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
        for (var i = 0; i < 7; i++)
        {
            _glowPoints[i].Color = (_patterns[num] & (1 << i)) != 0 ? _color : Color.Transparent;
        }

        return false;
    }
}
