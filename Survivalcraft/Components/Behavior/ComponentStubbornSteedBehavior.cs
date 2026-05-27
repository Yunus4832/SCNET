using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentStubbornSteedBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private ComponentEatPickableBehavior _componentEatPickableBehavior = null!;

    private ComponentMount _componentMount = null!;

    private ComponentSteedBehavior _componentSteedBehavior = null!;

    private float _importanceLevel;

    private bool _isSaddled;

    private float _periodicEventOffset;

    private readonly Random _random = new();

    private double _stubbornEndTime;

    private float _stubbornProbability;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        stateMachine.Update();
        if (!IsActive)
        {
            stateMachine.TransitionTo("Inactive");
        }

        if (!_subsystemTime.PeriodicGameTimeEvent(1.0, _periodicEventOffset))
        {
            return;
        }

        if (_subsystemGameInfo.TotalElapsedGameTime < _stubbornEndTime &&
            _componentEatPickableBehavior.Satiation <= 0f && _componentMount.Rider != null)
        {
            _importanceLevel = 210f;
        }
        else
        {
            _importanceLevel = 0f;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentMount = Entity.FindComponent<ComponentMount>(true)!;
        _componentSteedBehavior = Entity.FindComponent<ComponentSteedBehavior>(true)!;
        _componentEatPickableBehavior = Entity.FindComponent<ComponentEatPickableBehavior>(true)!;
        _stubbornProbability = valuesDictionary.GetValue<float>("StubbornProbability");
        _stubbornEndTime = valuesDictionary.GetValue<double>("StubbornEndTime");
        _periodicEventOffset = _random.Float(0f, 100f);
        _isSaddled = Entity.ValuesDictionary.DatabaseObject.Name.EndsWith("_Saddled");
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (_subsystemTime.PeriodicGameTimeEvent(1.0, _periodicEventOffset) && _componentMount.Rider != null &&
                    _random.Float(0f, 1f) < _stubbornProbability &&
                    (!_isSaddled || _componentEatPickableBehavior.Satiation <= 0f))
                {
                    _stubbornEndTime = _subsystemGameInfo.TotalElapsedGameTime + _random.Float(60f, 120f);
                }

                if (IsActive)
                {
                    stateMachine.TransitionTo("Stubborn");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stubborn",
            Actions.Empty,
            delegate
            {
                if (!_componentSteedBehavior.WasOrderIssued)
                {
                    return;
                }

                _componentCreature.ComponentCreatureModel.HeadShakeOrder = _random.Float(0.6f, 1f);
                _componentCreature.ComponentCreatureSounds.PlayPainSound();
            },
            Actions.Empty
        );
        stateMachine.TransitionTo("Inactive");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("StubbornEndTime", _stubbornEndTime);
    }
}
