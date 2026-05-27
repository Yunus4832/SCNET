using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Components;

public class ComponentLayEggBehavior : ComponentBehavior, IUpdateable
{
    private ComponentCreature _componentCreature = null!;

    private ComponentPathfinding _componentPathfinding = null!;

    private float _dt;

    private EggBlock.EggType? _eggType;

    private float _importanceLevel;

    private float _layFrequency;

    private float _layTime;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public override float ImportanceLevel => _importanceLevel;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (string.IsNullOrEmpty(stateMachine.CurrentState))
        {
            stateMachine.TransitionTo("Move");
        }

        if (_eggType != null && _random.Float(0f, 1f) < _layFrequency * dt)
        {
            _importanceLevel = _random.Float(1f, 2f);
        }

        _dt = dt;
        if (IsActive)
        {
            stateMachine.Update();
        }
        else
        {
            stateMachine.TransitionTo("Inactive");
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPathfinding = Entity.FindComponent<ComponentPathfinding>(true)!;
        var eggBlock = (EggBlock)BlocksManager.Blocks[118];
        _layFrequency = valuesDictionary.GetValue<float>("LayFrequency");
        _eggType = eggBlock.GetEggTypeByCreatureTemplateName(Entity.ValuesDictionary.DatabaseObject.Name);
        stateMachine.AddState(
            "Inactive",
            Actions.Empty,
            delegate
            {
                if (IsActive)
                {
                    stateMachine.TransitionTo("Move");
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Stuck",
            delegate { stateMachine.TransitionTo("Move"); },
            Actions.Empty,
            Actions.Empty
        );
        stateMachine.AddState(
            "Move",
            delegate
            {
                var position2 = _componentCreature.ComponentBody.Position;
                const float num = 5f;
                var value3 = position2 + new Vector3(num * _random.Float(-1f, 1f), 0f, num * _random.Float(-1f, 1f));
                value3.Y = _subsystemTerrain.Terrain.GetTopHeight(Terrain.ToCell(value3.X), Terrain.ToCell(value3.Z)) +
                           1;
                _componentPathfinding.SetDestination(value3, _random.Float(0.4f, 0.6f), 0.5f, 0, false, true, false,
                    null);
            },
            delegate
            {
                if (!_componentPathfinding.Destination.HasValue)
                {
                    stateMachine.TransitionTo("Lay");
                }
                else if (_componentPathfinding.IsStuck)
                {
                    if (_random.Float(0f, 1f) < 0.5f)
                    {
                        stateMachine.TransitionTo("Stuck");
                    }
                    else
                    {
                        _importanceLevel = 0f;
                    }
                }
            },
            Actions.Empty
        );
        stateMachine.AddState(
            "Lay",
            delegate { _layTime = 0f; },
            delegate
            {
                if (_eggType != null)
                {
                    _layTime += _dt;
                    if (_componentCreature.ComponentBody.StandingOnValue.HasValue)
                    {
                        _componentCreature.ComponentLocomotion.LookOrder =
                            new Vector2(0f,
                                0.25f * (float)MathUtils.Sin(20.0 * _subsystemTime.GameTime) + _layTime / 3f) -
                            _componentCreature.ComponentLocomotion.LookAngles;
                        if (!(_layTime >= 3f))
                        {
                            return;
                        }

                        //客户端禁止下蛋
                        if (CommonLib.WorkType == WorkType.Client)
                        {
                            return;
                        }

                        _importanceLevel = 0f;
                        var value = Terrain.MakeBlockValue(118, 0,
                            EggBlock.SetIsLaid(EggBlock.SetEggType(0, _eggType.EggTypeIndex), true));
                        var matrix = _componentCreature.ComponentBody.Matrix;
                        var position = 0.5f * (_componentCreature.ComponentBody.BoundingBox.Min +
                                               _componentCreature.ComponentBody.BoundingBox.Max);
                        var value2 = 3f * Vector3.Normalize(-matrix.Forward + 0.1f * matrix.Up +
                                                            0.2f * _random.Float(-1f, 1f) * matrix.Right);
                        _subsystemPickables.AddPickable(value, 1, position, value2, null);
                        _subsystemAudio.PlaySound("Audio/EggLaid", 1f, _random.Float(-0.1f, 0.1f), position, 2f,
                            true);
                    }
                    else if (_layTime >= 3f)
                    {
                        _importanceLevel = 0f;
                    }
                }
                else
                {
                    _importanceLevel = 0f;
                }
            },
            Actions.Empty
        );
    }
}
