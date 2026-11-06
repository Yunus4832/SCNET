using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemFirePresentation : Subsystem, IUpdateable
{
    private readonly Random _random = new();

    private readonly DynamicArray<Point3> _firePointsCopy = new();

    private int _copyIndex;

    private float _remainderToScan;

    private float _fireSoundIntensity;

    private float _fireSoundVolume;

    private int _effectFrame = -1;

    private int _debrisCount;

    private int _sizzleCount;

    private bool _disposed;

    private SubsystemFireBlockBehavior _fire = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemAmbientSounds _subsystemAmbientSounds = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _fire = Project.FindSubsystem<SubsystemFireBlockBehavior>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemAmbientSounds = Project.FindSubsystem<SubsystemAmbientSounds>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
    }

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (_firePointsCopy.Count == 0)
        {
            _firePointsCopy.Count = _fire.FirePoints.Count;
            var index = 0;
            foreach (var point in _fire.FirePoints) { _firePointsCopy.Array[index++] = point; }

            _copyIndex = 0;
            if (_firePointsCopy.Count == 0) { _fireSoundVolume = 0f; }
        }

        var budget = MathUtils.Min(dt * _firePointsCopy.Count + _remainderToScan, 50f);
        var count = (int)budget;
        _remainderToScan = budget - count;
        var end = MathUtils.Min(_copyIndex + count, _firePointsCopy.Count);
        while (_copyIndex < end)
        {
            var point = _firePointsCopy.Array[_copyIndex++];
            if (_fire.ContainsFire(point))
            {
                _fireSoundIntensity +=
                    1f / (_subsystemAudio.CalculateListenerDistanceSquared(new Vector3(point)) + 0.01f);
            }
        }

        if (_copyIndex >= _firePointsCopy.Count)
        {
            _fireSoundVolume = 0.75f * _fireSoundIntensity;
            _fireSoundIntensity = 0f;
            _firePointsCopy.Clear();
        }

        _subsystemAmbientSounds.FireSoundVolume =
            MathUtils.Max(_subsystemAmbientSounds.FireSoundVolume, _fireSoundVolume);
    }

    internal void PlayBurnEffect(Vector3 position, BlockBurnEffectKind kind)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (_effectFrame != Time.FrameIndex)
        {
            _effectFrame = Time.FrameIndex;
            _debrisCount = 0;
            _sizzleCount = 0;
        }

        var distance = _subsystemViews.CalculateDistanceFromNearestView(position);
        if (kind != BlockBurnEffectKind.FluidSizzle && _debrisCount < 15 && distance < 24f)
        {
            _subsystemParticles.AddParticleSystem(new BurntDebrisParticleSystem(_terrain, position));
            _debrisCount++;
        }

        if (kind != BlockBurnEffectKind.Debris && _sizzleCount < 4 && distance < 16f)
        {
            var pitchRange = kind == BlockBurnEffectKind.FluidSizzle ? 0.1f : 0.25f;
            var radius = kind == BlockBurnEffectKind.FluidSizzle ? 5f : 3f;
            _subsystemAudio.PlayRandomSound(
                "Audio/Sizzles",
                1f,
                _random.Float(-pitchRange, pitchRange),
                position,
                radius,
                true
            );
            _sizzleCount++;
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _firePointsCopy.Clear();
        _fireSoundIntensity = 0f;
        _fireSoundVolume = 0f;
        base.Dispose();
    }
}
