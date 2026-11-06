using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public readonly record struct SoundGeneratorNote(
    [property: Key(0)] Point3 Point,
    [property: Key(1)] int Face,
    [property: Key(2)] SoundGeneratorInstrument Instrument,
    [property: Key(3)] int SampleOctave,
    [property: Key(4)] int NoteInput,
    [property: Key(5)] int VolumeInput,
    [property: Key(6)] float Pitch
)
{
    [IgnoreMember] public float Volume => VolumeInput / 15f;

    [IgnoreMember] public float MinDistance => 0.5f + 5f * VolumeInput / 15f;

    [IgnoreMember]
    public bool IsValid => Face is >= 0 and < 6 &&
                           Point.Y is >= 0 and < 256 &&
                           NoteInput is >= 0 and < 15 &&
                           VolumeInput is >= 0 and <= 15 &&
                           float.IsFinite(Pitch) &&
                           Pitch is >= -1f and <= 1f &&
                           (Instrument == SoundGeneratorInstrument.Drums
                               ? SampleOctave == 0 && NoteInput < 10
                               : SampleOctave >= 4 && SampleOctave <= GetMaxSampleOctave(Instrument));

    public static int GetMaxSampleOctave(SoundGeneratorInstrument instrument)
    {
        return instrument switch
        {
            SoundGeneratorInstrument.Organ => 5,
            SoundGeneratorInstrument.Bell or SoundGeneratorInstrument.Ping or SoundGeneratorInstrument.String or
                SoundGeneratorInstrument.Trumpet or SoundGeneratorInstrument.Voice or SoundGeneratorInstrument.Piano or
                SoundGeneratorInstrument.PianoLong or SoundGeneratorInstrument.Bass
                or SoundGeneratorInstrument.AlternatePiano => 6,
            _ => 0
        };
    }
}
