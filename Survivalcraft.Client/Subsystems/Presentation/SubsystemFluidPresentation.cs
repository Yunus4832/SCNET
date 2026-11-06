using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemFluidPresentation : Subsystem, IUpdateable
{
    private SubsystemWaterBlockBehavior _water = null!;

    private SubsystemMagmaBlockBehavior _magma = null!;

    private SubsystemTime _time = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemAmbientSounds _ambient = null!;

    private float _waterSoundVolume;

    private float _magmaSoundVolume;

    private bool _disposed;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _water = Project.FindSubsystem<SubsystemWaterBlockBehavior>(true)!;
        _magma = Project.FindSubsystem<SubsystemMagmaBlockBehavior>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _ambient = Project.FindSubsystem<SubsystemAmbientSounds>(true)!;
    }

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (_time.PeriodicGameTimeEvent(1.0, 0.25))
        {
            var num = float.MaxValue;
            foreach (var listenerPosition in _audio.ListenerPositions)
            {
                var num2 = _water.CalculateDistanceToFluid(listenerPosition, 8, true);
                if (num2 < num)
                {
                    num = num2.Value;
                }
            }

            _waterSoundVolume = 0.5f * _audio.CalculateVolume(num, 2f, 3.5f);
        }

        _ambient.WaterSoundVolume = MathUtils.Max(_ambient.WaterSoundVolume, _waterSoundVolume);
        if (_time.PeriodicGameTimeEvent(1.0, 0.75))
        {
            var num = float.MaxValue;
            foreach (var listenerPosition in _audio.ListenerPositions)
            {
                var num2 = _magma.CalculateDistanceToFluid(listenerPosition, 8, false);
                if (num2.HasValue && num2.Value < num)
                {
                    num = num2.Value;
                }
            }

            _magmaSoundVolume = _audio.CalculateVolume(num, 2f, 3.5f);
        }

        _ambient.MagmaSoundVolume = MathUtils.Max(_ambient.MagmaSoundVolume, _magmaSoundVolume);
    }

    public override void Dispose()
    {
        _disposed = true;
        _waterSoundVolume = 0f;
        _magmaSoundVolume = 0f;
        base.Dispose();
    }
}
