namespace Game.ElectricElements;

public class SoundGeneratorElectricElement : RotateableElectricElement
{
    private readonly string[] _drums =
    [
        "Snare",
        "BassDrum",
        "ClosedHiHat",
        "PedalHiHat",
        "OpenHiHat",
        "LowTom",
        "HighTom",
        "CrashCymbal",
        "RideCymbal",
        "HandClap"
    ];

    private int _lastToneInput;

    private readonly int[] _maxOctaves = [0, 6, 5, 6, 6, 6, 6, 6, 6, 0, 6, 0, 0, 0, 0, 6];

    private readonly SoundParticleSystem _particleSystem;

    private double _playAllowedTime;

    private readonly Random _random = new();

    private readonly SubsystemNoise _subsystemNoise;

    private readonly SubsystemParticles _subsystemParticles;

    private readonly string[] _tones =
    [
        "",
        "Bell",
        "Organ",
        "Ping",
        "String",
        "Trumpet",
        "Voice",
        "Piano",
        "PianoLong",
        "Drums",
        "Bass",
        "",
        "",
        "",
        "",
        "Piano"
    ];

    public SoundGeneratorElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _subsystemNoise = subsystemElectricity.Project.FindSubsystem<SubsystemNoise>(true)!;
        _subsystemParticles = subsystemElectricity.Project.FindSubsystem<SubsystemParticles>(true)!;
        var vector = CellFace.FaceToVector3(cellFace.Face);
        var position = new Vector3(cellFace.Point) + new Vector3(0.5f) - 0.2f * vector;
        _particleSystem = new SoundParticleSystem(subsystemElectricity.SubsystemTerrain, position, vector);
    }

    public override bool Simulate()
    {
        var num = 0;
        var num2 = 15;
        var num3 = 2;
        var num4 = 0;
        var rotation = Rotation;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                var connectorDirection =
                    SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, rotation, connection.ConnectorFace);
                if (!connectorDirection.HasValue)
                {
                    continue;
                }

                if (connectorDirection.Value is ElectricConnectorDirection.In or ElectricConnectorDirection.Bottom)
                {
                    num4 = (int)MathUtils.Round(15f *
                                                connection.NeighborElectricElement.GetOutputVoltage(connection
                                                    .NeighborConnectorFace));
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Left)
                {
                    num = (int)MathUtils.Round(15f *
                                               connection.NeighborElectricElement.GetOutputVoltage(connection
                                                   .NeighborConnectorFace));
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Right)
                {
                    num3 = (int)MathUtils.Round(15f *
                                                connection.NeighborElectricElement.GetOutputVoltage(connection
                                                    .NeighborConnectorFace));
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Top)
                {
                    num2 = (int)MathUtils.Round(15f *
                                                connection.NeighborElectricElement.GetOutputVoltage(connection
                                                    .NeighborConnectorFace));
                }
            }
        }

        if (_lastToneInput == 0 && num4 != 0 && num != 15 &&
            SubsystemElectricity.SubsystemTime.GameTime >= _playAllowedTime)
        {
            _playAllowedTime = SubsystemElectricity.SubsystemTime.GameTime + 0.079999998211860657;
            var text = _tones[num4];
            var num5 = 0f;
            string? text2 = null;
            if (text == "Drums")
            {
                num5 = 1f;
                if (num >= 0 && num < _drums.Length)
                {
                    text2 = $"Audio/SoundGenerator/Drums{_drums[num]}";
                }
            }
            else if (!string.IsNullOrEmpty(text))
            {
                var num6 = 130.8125f * MathUtils.Pow(1.05946314f, num + 12 * num3);
                var num7 = 0;
                for (var i = 4; i <= _maxOctaves[num4]; i++)
                {
                    var num8 = num6 / (523.25f * MathUtils.Pow(2f, i - 5));
                    if (num7 != 0 && (!(num8 >= 0.5f) || !(num8 < num5)))
                    {
                        continue;
                    }

                    num7 = i;
                    num5 = num8;
                }

                text2 = $"Audio/SoundGenerator/{text}C{num7}";
            }

            if (num5 != 0f && !string.IsNullOrEmpty(text2))
            {
                var cellFace = CellFaces[0];
                var position = new Vector3(cellFace.X, cellFace.Y, cellFace.Z);
                var volume = num2 / 15f;
                var pitch = MathUtils.Clamp(MathUtils.Log(num5) / MathUtils.Log(2f), -1f, 1f);
                var minDistance = 0.5f + 5f * num2 / 15f;
                SubsystemElectricity.SubsystemAudio.PlaySound(text2, volume, pitch, position, minDistance, true);
                var loudness = num2 < 8 ? 0.25f : 0.5f;
                var range = MathUtils.Lerp(2f, 20f, num2 / 15f);
                _subsystemNoise.MakeNoise(position, loudness, range);
                if (_particleSystem.SubsystemParticles == null)
                {
                    _subsystemParticles.AddParticleSystem(_particleSystem);
                }

                var hsv = new Vector3(22.5f * num + _random.Float(0f, 22f), 0.5f + num2 / 30f, 1f);
                _particleSystem.AddNote(new Color(Color.HsvToRgb(hsv)));
            }
        }

        _lastToneInput = num4;
        return false;
    }
}
