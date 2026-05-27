using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentLookAroundBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private float _importanceLevel;

    private float _lookAroundTime;

    private readonly Random _random = new();

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;

        stateMachine.AddState(
            "Inactive",
            delegate { _importanceLevel = _random.Float(0f, 1f); },
            delegate
            {
                if (_componentCreature.ComponentBody.StandingOnValue.HasValue &&
                    _random.Float(0f, 1f) < 0.05f * _subsystemTime.GameTimeDelta)
                {
                    _importanceLevel = _random.Float(1f, 5f);
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("LookAround");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "LookAround",
            delegate { _lookAroundTime = _random.Float(8f, 15f); },
            delegate
            {
                if (!IsActive)
                {
                    stateMachine.TransitionTo("Inactive");
                }
                else if (_lookAroundTime <= 0f)
                {
                    _importanceLevel = 0f;
                }
                else if (_random.Float(0f, 1f) < 0.1f * _subsystemTime.GameTimeDelta)
                {
                    _componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                }

                _componentCreature.ComponentCreatureModel.LookRandomOrder = true;
                _lookAroundTime -= _subsystemTime.GameTimeDelta;
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }
}
