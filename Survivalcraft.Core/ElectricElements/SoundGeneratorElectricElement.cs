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

        var noteCode = 0;
        var volumeLevel = 15;
        var octave = 2;
        var instrumentCode = 0;
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
                    instrumentCode = (int)MathUtils.Round(
                        15f *
                        connection.NeighborElectricElement.GetOutputVoltage(
                            connection
                                .NeighborConnectorFace
                        )
                    );
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Left)
                {
                    noteCode = (int)MathUtils.Round(
                        15f *
                        connection.NeighborElectricElement.GetOutputVoltage(
                            connection
                                .NeighborConnectorFace
                        )
                    );
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Right)
                {
                    octave = (int)MathUtils.Round(
                        15f *
                        connection.NeighborElectricElement.GetOutputVoltage(
                            connection
                                .NeighborConnectorFace
                        )
                    );
                }
                else if (connectorDirection.Value == ElectricConnectorDirection.Top)
                {
                    volumeLevel = (int)MathUtils.Round(
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
            instrumentCode != 0 &&
            noteCode != 15 &&
            SubsystemElectricity.SubsystemTime.GameTime >= _playAllowedTime)
        {
            _playAllowedTime = SubsystemElectricity.SubsystemTime.GameTime + 0.079999998211860657;
            var instrument = (SoundGeneratorInstrument)instrumentCode;
            var sampleOctave = 0;
            var pitchRatio = 0f;
            var hasSample = false;
            if (instrument == SoundGeneratorInstrument.Drums)
            {
                pitchRatio = 1f;
                if (noteCode >= 0 && noteCode < 10)
                {
                    hasSample = true;
                }
            }
            else if (SoundGeneratorNote.GetMaxSampleOctave(instrument) > 0)
            {
                var targetFrequency = 130.8125f * MathUtils.Pow(1.05946314f, noteCode + 12 * octave);
                var selectedOctave = 0;
                for (var i = 4; i <= SoundGeneratorNote.GetMaxSampleOctave(instrument); i++)
                {
                    var candidatePitchRatio = targetFrequency / (523.25f * MathUtils.Pow(2f, i - 5));
                    if (selectedOctave != 0 && (!(candidatePitchRatio >= 0.5f) || !(candidatePitchRatio < pitchRatio)))
                    {
                        continue;
                    }

                    selectedOctave = i;
                    pitchRatio = candidatePitchRatio;
                }

                sampleOctave = selectedOctave;
                hasSample = true;
            }

            if (pitchRatio != 0f && hasSample)
            {
                var cellFace = CellFaces[0];
                var position = new Vector3(cellFace.X, cellFace.Y, cellFace.Z);
                var pitch = MathUtils.Clamp(MathUtils.Log(pitchRatio) / MathUtils.Log(2f), -1f, 1f);
                var note = new SoundGeneratorNote(
                    cellFace.Point,
                    cellFace.Face,
                    instrument,
                    sampleOctave,
                    noteCode,
                    volumeLevel,
                    pitch
                );
                if (!note.IsValid)
                {
                    _lastToneInput = instrumentCode;
                    return false;
                }

                SubsystemElectricity.PublishSoundGeneratorNote(note);
                var loudness = volumeLevel < 8 ? 0.25f : 0.5f;
                var range = MathUtils.Lerp(2f, 20f, volumeLevel / 15f);
                _subsystemNoise.MakeNoise(position, loudness, range);
            }
        }

        _lastToneInput = instrumentCode;
        return false;
    }
}
