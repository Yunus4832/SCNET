using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentHealthPresentation : Component, IUpdateable
{
    private ComponentCreature _creature = null!;

    private ComponentHealth _health = null!;

    private ComponentCharacter? _character;

    private SubsystemParticles _particles = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemTime _time = null!;

    private float _lastHealth;

    private float _redScreenFactor;

    private double? _crushWarningTime;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels + 1;

    public void Update(float dt)
    {
        if (!IsAddedToProject || !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var change = _health.Health - _lastHealth;
        _lastHealth = _health.Health;
        _redScreenFactor = _redScreenFactor > 0.01f ? _redScreenFactor * MathUtils.Pow(0.2f, dt) : 0f;
        if (change < 0f)
        {
            _creature.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayPainSound();
            _redScreenFactor += -4f * change;
            if (_health.Health == 0f)
            {
                var body = _creature.ComponentBody;
                _particles.AddParticleSystem(
                    new KillParticleSystem(
                        _terrain,
                        body.Position + new Vector3(0f, body.BoxSize.Y / 2f, 0f),
                        body.StanceBoxSize.X
                    )
                );
            }
        }

        if (_character is not { IsLocallyControlled: true }) { return; }

        if (change < 0f)
        {
            _character.Entity.FindComponent<ComponentGui>(true)!.HealthBarWidget.Flash(
                MathUtils.Clamp((int)(-change * 30f), 0, 10)
            );
        }

        var characterBody = _character.ComponentBody;
        if (characterBody.Position.Y < 0f && _time.PeriodicGameTimeEvent(2.0, 0.0))
        {
            _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                Localization.ClientLanguage.Catalog.Get(ComponentHealth.Name, 4),
                Color.White,
                true,
                false
            );
        }

        if (characterBody is { ImmersionFactor: > 0f, ImmersionFluidBlock: MagmaBlock })
        {
            var pulse = 1.1f + 0.1f * (float)MathUtils.Sin(12.0 * _time.GameTime);
            _redScreenFactor = MathUtils.Max(_redScreenFactor, pulse * 1.5f * characterBody.ImmersionFactor);
        }

        if (characterBody.IsCrushed)
        {
            _redScreenFactor = MathUtils.Max(_redScreenFactor, 1f);
            _crushWarningTime ??= _time.GameTime + 1;
            if (_time.GameTime >= _crushWarningTime)
            {
                _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                    "你卡住了",
                    Color.White,
                    true,
                    true
                );
                _crushWarningTime = _time.GameTime + 1;
            }
        }
        else { _crushWarningTime = null; }

        _character.Entity.FindComponent<ComponentScreenOverlays>(true)!.RedOutFactor = MathUtils.Max(
            _character.Entity.FindComponent<ComponentScreenOverlays>(true)!.RedOutFactor,
            _redScreenFactor
        );
        _character.Entity.FindComponent<ComponentGui>(true)!.HealthBarWidget.Value = _health.Health;
    }

    internal static void PlayHitEffect(Project project, CharacterHitEffectPackage effect)
    {
        if (!WorldRuntimeContext.IsProjectActive(project) || !WorldRuntimeContext.For(project).HasPresentation)
        {
            return;
        }

        var text = Localization.ClientLanguage.Resolver.Resolve(effect.Text);
        if (!string.IsNullOrEmpty(text))
        {
            project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(
                new HitValueParticleSystem(effect.Position, effect.Velocity, effect.Color, text)
            );
        }

        if (effect.PlayBodyImpact)
        {
            project.FindSubsystem<SubsystemAudio>(true)!.PlayRandomSound(
                "Audio/Impacts/Body",
                1f,
                new Random().Float(-.3f, .3f),
                effect.BodyPosition,
                4f,
                false
            );
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _creature = Entity.FindComponent<ComponentCreature>(true)!;
        _health = Entity.FindComponent<ComponentHealth>(true)!;
        _character = Entity.FindComponent<ComponentCharacter>();
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _lastHealth = _health.Health;
    }
}
