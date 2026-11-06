using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components.Behavior;

public class ComponentHowlBehavior : ComponentBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _howlDuration;

    private float _howlTime;

    private float _importanceLevel;

    private readonly Random _random = new();

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        stateMachine.AddState(
            "Inactive",
            delegate { _importanceLevel = 0f; },
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Howl");
                }

                if (_subsystemSky.SkyLightIntensity < 0.1f)
                {
                    if (_random.Float(0f, 1f) < 0.015f * _subsystemTime.GameTimeDelta)
                    {
                        _importanceLevel = _random.Float(1f, 3f);
                    }
                }
                else
                {
                    _importanceLevel = 0f;
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Howl",
            delegate
            {
                _howlTime = 0f;
                _howlDuration = _random.Float(5f, 6f);
                _componentPathfinding.Stop();
                _importanceLevel = 10f;
            },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }

                _componentCreature.ComponentLocomotion.LookOrder =
                    new Vector2(_componentCreature.ComponentLocomotion.LookOrder.X, 2f);
                var num = _howlTime + _subsystemTime.GameTimeDelta;
                if (_howlTime <= 0.5f && num > 0.5f)
                {
                    _componentCreature.ComponentCreatureSounds.PlayHowlSound();
                }

                _howlTime = num;
                if (_howlTime >= _howlDuration)
                {
                    _importanceLevel = 0f;
                }
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }
}
