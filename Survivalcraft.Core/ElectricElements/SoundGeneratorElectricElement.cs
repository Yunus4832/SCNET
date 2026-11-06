namespace Game.ElectricElements;

public class SoundGeneratorElectricElement : RotatableElectricElement
{
    private int _lastToneInput;


    private double _playAllowedTime;


    private readonly SubsystemNoise _subsystemNoise;


    public SoundGeneratorElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _subsystemNoise = subsystemElectricity.Project.FindSubsystem<SubsystemNoise>(true)!;
    }

    public override bool Simulate()
    {
        if (!WorldRuntimeContext.For(SubsystemElectricity.Project).IsAuthority) { return false; }

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
                    num4 = (int)MathUtils.Round(
                        15f *
                        connection.NeighborElectricElement.GetOutputVoltage(
                            connection
                                .NeighborConnectorFace
                        )
                    );
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Left)
                {
                    num = (int)MathUtils.Round(
                        15f *
                        connection.NeighborElectricElement.GetOutputVoltage(
                            connection
                                .NeighborConnectorFace
                        )
                    );
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Right)
                {
                    num3 = (int)MathUtils.Round(
                        15f *
                        connection.NeighborElectricElement.GetOutputVoltage(
                            connection
                                .NeighborConnectorFace
                        )
                    );
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Top)
                {
                    num2 = (int)MathUtils.Round(
                        15f *
                        connection.NeighborElectricElement.GetOutputVoltage(
                            connection
                                .NeighborConnectorFace
                        )
                    );
                }
            }
        }

        if (_lastToneInput == 0 &&
            num4 != 0 &&
            num != 15 &&
            SubsystemElectricity.SubsystemTime.GameTime >= _playAllowedTime)
        {
            _playAllowedTime = SubsystemElectricity.SubsystemTime.GameTime + 0.079999998211860657;
            var instrument = (SoundGeneratorInstrument)num4;
            var sampleOctave = 0;
            var num5 = 0f;
            var hasSample = false;
            if (instrument == SoundGeneratorInstrument.Drums)
            {
                num5 = 1f;
                if (num >= 0 && num < 10)
                {
                    hasSample = true;
                }
            }
            else if (SoundGeneratorNote.GetMaxSampleOctave(instrument) > 0)
            {
                var num6 = 130.8125f * MathUtils.Pow(1.05946314f, num + 12 * num3);
                var num7 = 0;
                for (var i = 4; i <= SoundGeneratorNote.GetMaxSampleOctave(instrument); i++)
                {
                    var num8 = num6 / (523.25f * MathUtils.Pow(2f, i - 5));
                    if (num7 != 0 && (!(num8 >= 0.5f) || !(num8 < num5)))
                    {
                        continue;
                    }

                    num7 = i;
                    num5 = num8;
                }

                sampleOctave = num7;
                hasSample = true;
            }

            if (num5 != 0f && hasSample)
            {
                var cellFace = CellFaces[0];
                var position = new Vector3(cellFace.X, cellFace.Y, cellFace.Z);
                var pitch = MathUtils.Clamp(MathUtils.Log(num5) / MathUtils.Log(2f), -1f, 1f);
                var note = new SoundGeneratorNote(
                    cellFace.Point,
                    cellFace.Face,
                    instrument,
                    sampleOctave,
                    num,
                    num2,
                    pitch
                );
                if (!note.IsValid)
                {
                    _lastToneInput = num4;
                    return false;
                }

                SubsystemElectricity.PublishSoundGeneratorNote(note);
                var loudness = num2 < 8 ? 0.25f : 0.5f;
                var range = MathUtils.Lerp(2f, 20f, num2 / 15f);
                _subsystemNoise.MakeNoise(position, loudness, range);
            }
        }

        _lastToneInput = num4;
        return false;
    }
}
