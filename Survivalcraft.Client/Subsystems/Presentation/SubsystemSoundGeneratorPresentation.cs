using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemSoundGeneratorPresentation : Subsystem
{
    private static readonly string[] _drums =
    [
        "Snare", "BassDrum", "ClosedHiHat", "PedalHiHat", "OpenHiHat", "LowTom", "HighTom", "CrashCymbal", "RideCymbal",
        "HandClap"
    ];

    private readonly Dictionary<(Point3 Point, int Face), SoundParticleSystem> _emitters = new();

    private readonly Random _visualRandom = new();

    private SubsystemElectricity _electricity = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private bool _disposed;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _electricity = Project.FindSubsystem<SubsystemElectricity>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _electricity.ElectricBlockRemoved += RemoveBlockEffects;
        _electricity.ElectricChunkDiscarded += ClearChunkEffects;
        _terrain.Closing += OnTerrainClosing;
    }

    internal static string GetSoundName(SoundGeneratorNote note)
    {
        if (!note.IsValid) { throw new ArgumentException("Invalid sound generator note.", nameof(note)); }

        if (note.Instrument == SoundGeneratorInstrument.Drums)
        {
            return "Audio/SoundGenerator/Drums" + _drums[note.NoteInput];
        }

        var instrument = note.Instrument switch
        {
            SoundGeneratorInstrument.Bell => "Bell",
            SoundGeneratorInstrument.Organ => "Organ",
            SoundGeneratorInstrument.Ping => "Ping",
            SoundGeneratorInstrument.String => "String",
            SoundGeneratorInstrument.Trumpet => "Trumpet",
            SoundGeneratorInstrument.Voice => "Voice",
            SoundGeneratorInstrument.Piano or SoundGeneratorInstrument.AlternatePiano => "Piano",
            SoundGeneratorInstrument.PianoLong => "PianoLong",
            SoundGeneratorInstrument.Bass => "Bass",
            _ => throw new ArgumentOutOfRangeException(nameof(note))
        };
        return "Audio/SoundGenerator/" +
               instrument +
               "C" +
               note.SampleOctave.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public void PlayNote(SoundGeneratorNote note)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project) || !note.IsValid) { return; }

        _audio.PlaySound(GetSoundName(note), note.Volume, note.Pitch, new Vector3(note.Point), note.MinDistance, true);
        var key = (note.Point, note.Face);
        if (!_emitters.TryGetValue(key, out var emitter))
        {
            var direction = CellFace.FaceToVector3(note.Face);
            var position = new Vector3(note.Point) + new Vector3(.5f) - .2f * direction;
            emitter = new SoundParticleSystem(_terrain, position, direction);
            _emitters.Add(key, emitter);
        }

        if (emitter.SubsystemParticles is null) { _particles.AddParticleSystem(emitter); }

        var hsv = new Vector3(22.5f * note.NoteInput + _visualRandom.Float(0f, 22f), .5f + note.VolumeInput / 30f, 1f);
        emitter.AddNote(new Color(Color.HsvToRgb(hsv)));
    }

    private void RemoveEmitter((Point3 Point, int Face) key)
    {
        if (_emitters.Remove(key, out var emitter) && ReferenceEquals(emitter.SubsystemParticles, _particles))
        {
            _particles.RemoveParticleSystem(emitter);
        }
    }

    private void RemoveBlockEffects(Point3 point)
    {
        foreach (var key in _emitters.Keys.Where(key => key.Point == point).ToArray()) { RemoveEmitter(key); }
    }

    public void ClearChunkEffects(Point2 chunk)
    {
        foreach (var key in _emitters.Keys.Where(key => key.Point.X >> 4 == chunk.X && key.Point.Z >> 4 == chunk.Y)
                     .ToArray())
        {
            RemoveEmitter(key);
        }
    }

    private void OnTerrainClosing(SubsystemTerrain terrain) => Dispose();

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_electricity is not null)
        {
            _electricity.ElectricBlockRemoved -= RemoveBlockEffects;
            _electricity.ElectricChunkDiscarded -= ClearChunkEffects;
        }

        if (_terrain is not null) { _terrain.Closing -= OnTerrainClosing; }

        foreach (var key in _emitters.Keys.ToArray()) { RemoveEmitter(key); }

        base.Dispose();
    }
}
