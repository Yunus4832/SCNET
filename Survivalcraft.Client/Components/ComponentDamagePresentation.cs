using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentDamagePresentation : Component
{
    private ComponentBody _body = null!;

    private ComponentDamage? _damage;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private SubsystemTerrain _terrain = null!;

    private float _debrisScale;

    private float _debrisStrength;

    private int _debrisTextureSlot;

    private string _destructionSoundName = string.Empty;

    private bool _subscribed;

    private readonly Random _random = new();

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _body = Entity.FindComponent<ComponentBody>(true)!;
        _damage = Entity.FindComponent<ComponentDamage>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _debrisTextureSlot = valuesDictionary.GetValue<int>("DebrisTextureSlot");
        _debrisStrength = valuesDictionary.GetValue<float>("DebrisStrength");
        _debrisScale = valuesDictionary.GetValue<float>("DebrisScale");
        _destructionSoundName = valuesDictionary.GetValue<string>("DestructionSoundName");
        Subscribe();
    }

    public override void OnEntityAdded() => Subscribe();

    public override void OnEntityRemoved() => Unsubscribe();

    public override void Dispose() => Unsubscribe();

    private void Subscribe()
    {
        if (_damage is null || _subscribed) { return; }

        _damage.Destroyed += OnDestroyed;
        _damage.Attacked += OnAttacked;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_damage is null || !_subscribed) { return; }

        _damage.Destroyed -= OnDestroyed;
        _damage.Attacked -= OnAttacked;
        _subscribed = false;
    }

    private void OnAttacked()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        _audio.PlayRandomSound(_destructionSoundName, 1f, _random.Float(-0.3f, 0.3f), _body.Position, 4f, false);
    }

    private void OnDestroyed()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        _particles.AddParticleSystem(
            new BlockDebrisParticleSystem(
                _terrain,
                _body.Position + _body.StanceBoxSize.Y / 2f * Vector3.UnitY,
                _debrisStrength,
                _debrisScale,
                Color.White,
                _debrisTextureSlot
            )
        );
        _audio.PlayRandomSound(_destructionSoundName, 1f, 0f, _body.Position, 4f, true);
    }
}
